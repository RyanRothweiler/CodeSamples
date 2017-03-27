
/*
This is a gif encoder. It primarily supports aniamtions and allows the program to quickly build up the animation with a series of frames. 
Written for my custom engine. Allows in engine gif recording.
This is still dependent on some of my custom engine features (and my custom engine memory management). 
So it is not a truely reusable library.

This is written following this guide of the gif file format
http://www.matthewflickinger.com/lab/whatsinagif/bits_and_bytes.asp

Here is the actual spec
https://www.w3.org/Graphics/GIF/spec-gif89a.txt
*/

// NOTE the most patterns we can have is 4095
#define CodePattern_Max 999
#define Max_Patterns 4095

struct known_pattern
{
	uint8 CodePattern[1000];
	uint32 CodePatternLength;
};

struct compressed_output
{
	uint16* Output;
	uint8 NextBitOffset;
	uint8 CurrBitSize;

	uint16 CurrOutputIndex;
};

#pragma pack(push, 1)
struct color_table_index
{
	uint8 Red;
	uint8 Green;
	uint8 Blue;
};

struct image_descriptor
{
	uint8 ImageSeperator = 0x2C;

	// These are used because a frame may not take up the whole canvase size.
	uint16 ImageLeft;
	uint16 ImageTop;
	uint16 ImageWidth;
	uint16 ImageHeight;

	uint8 PackedField;
};

struct graphics_control_extension
{
	// Graphics Control Extension
	uint8 Introducer = 0x21;
	uint8 ControlLabel = 0xF9;
	uint8 ByteSize = 0x04;
	uint8 PackedField;
	// Time between frames hundredths of a second
	uint16 DelayTime;
	uint8 TransparentColorIndex;
	uint8 BlockTerminator = 0;
};

struct gif_header
{
	// Header and header
	uint8 SigHeader[6] = {'G', 'I', 'F', '8', '9', 'a'};

	// Logical screen Descriptor
	uint16 CanvasWidth;
	uint16 CanvasHeight;

	/*
	Pack field data | bits number (recommended data value)

	Global Color Table
	1 (1)

	Color Resolution (Number of bits per primary color minus 1 ??)
	2 (0)
	3 (0)
	4 (1)

	Sort  Flag
	5 (0)

	Size of Global Color Table 2^(N+1)
	6 (0)
	7 (0)
	8 (1)

	*/
	uint8 PackField;

	// What index into the global color table to use for the background color
	uint8 BackgroundColorIndex;

	uint8 PixelAspectRatio;

	// NOTE this 4 is defined in the pack field
	// TODO move this out of this struct, so it can be allocated dynamically based on the image
	color_table_index GlobalColorTable[4];

	// Application extension block (used for gif looping stuff)
	uint8 ApplicationExtensionIntroducer = 0x21;
	uint8 ExtensionLabel = 0xFF;
	uint8 ExtensionBlockSize = 0x0B;
	uint8 ApplicationIdentifier[11] = {'N', 'E', 'T', 'S', 'C', 'A', 'P', 'E', '2', '.', '0'};
	uint8 SubExtensionSize = 3;
	uint8 Initial = 0x01;
	uint8 LoopCount[2] = {0, 0};
	uint8 ExtensionTerminator = 0;
};
#pragma pack(pop)


struct frame
{
	graphics_control_extension GraphicsControlExtension;
	image_descriptor ImageDesc;

	compressed_output CompressedImageData;

	bool32 UsingLocalColorTable;
	color LocalColorTable[256];
	uint32 NextColTableIndex = 0;
	uint8 DebugCode;

	uint8 MinCodeSize;
};

enum disposal_method
{
	// No displosal method
	DisposalMethod_None = 0,

	// Leave the previous frame in place and draw the next one over it
	DisposalMethod_LeavePrevious = 1,

	// Restore to background color, then draw the next frame
	DisposalMethod_RestoreBackground = 2,
};

struct gif
{
	uint32 GlobalColorTableSize;

	uint16 Width;
	uint16 Height;

	frame Frames[100];
	uint32 NextFrame = 0;

	// The number of frames to wait before saving the game pixels
	uint32 FramesToWait;

	disposal_method FrameDisposalMethod;

	// NOTE this is here so we can share the patterns array for every frame added.
	// It is cleared to 0 at the start of LZW compression
	known_pattern* KnownPatterns;
};

void
AddPatternToOutput(compressed_output* Block, uint16 PatternIndex)
{
	uint8 LeftShift = Block->NextBitOffset;
	uint8 RightShift = 0;
	if (Block->NextBitOffset + Block->CurrBitSize >= 16)
	{
		RightShift = 16 - LeftShift;
		Block->NextBitOffset = Block->CurrBitSize - RightShift;
	}

	{
		uint16 SlidPattern = PatternIndex << LeftShift;
		Block->Output[Block->CurrOutputIndex] = (Block->Output[Block->CurrOutputIndex] | (uint16)SlidPattern);
	}

	if (RightShift > 0)
	{
		Block->CurrOutputIndex++;
		Assert(Block->CurrOutputIndex < 10000);

		uint16 SlidPattern = PatternIndex >> RightShift;
		Block->Output[Block->CurrOutputIndex] = (Block->Output[Block->CurrOutputIndex] | (uint16)SlidPattern);
	}
	else
	{
		Block->NextBitOffset += Block->CurrBitSize;
	}
}

// This uses LZW to compress a stream of color table indecies.
void
CompressImageData(compressed_output* Output, gif* Gif,
                  uint8* TableIndexIn, uint32 TableInSize,
                  memory_arena* TransientMem)
{
	ClearMemory((uint8*)Gif->KnownPatterns, sizeof(known_pattern) * Max_Patterns);
	known_pattern* KnownPatterns = Gif->KnownPatterns;

	frame* CurrFrame = &Gif->Frames[Gif->NextFrame];

	// init the color table
	{

		uint32 ColTableSize = 0;
		if (CurrFrame->UsingLocalColorTable)
		{
			ColTableSize = CurrFrame->NextColTableIndex;
		}
		else
		{
			ColTableSize = Gif->GlobalColorTableSize;
		}

		for (uint32 Index = 0; Index < ColTableSize; Index++)
		{
			KnownPatterns[Index].CodePatternLength = 1;
			KnownPatterns[Index].CodePattern[0] = (uint8)Index;
		}
	}

	uint8 FieldVal = CurrFrame->ImageDesc.PackedField & 0x07;
	uint8 ColTableSize = (uint8)Pow(2, FieldVal + 1);

	// add the Clear Code and End of Information code
	uint8 ClearCode = (uint8)ColTableSize;
	uint8 EOICode = (uint8)ColTableSize + 1;

	// NOTE this is replicted down below
	uint16 PatternNextIndex = (uint16)(ColTableSize + 2);

	// create code stream
	uint32 OutputSize = 10000;
	Output->Output = (uint16 *)ArenaAllocate(TransientMem, sizeof(uint16) * OutputSize);
	ClearMemory((uint8*)Output->Output, sizeof(uint16) * OutputSize);

	// NOTE this is replicated down below
	Output->CurrBitSize = CurrFrame->MinCodeSize + 1;
	Output->NextBitOffset = 0;

	KnownPatterns[ClearCode].CodePatternLength = 9999999;
	KnownPatterns[EOICode].CodePatternLength = 9999999;

	AddPatternToOutput(Output, ClearCode);

	uint32 IndexBufferIndexSize = 1000;
	uint8* IndexBuffer = (uint8 *)ArenaAllocate(TransientMem, IndexBufferIndexSize);
	ClearMemory(IndexBuffer, IndexBufferIndexSize);

	uint64 IndexBufferNext = 1;
	IndexBuffer[0] = TableIndexIn[0];

	for (uint64 CodeIndex = 1; CodeIndex < TableInSize; CodeIndex++)
	{
		uint8 NextCode = TableIndexIn[CodeIndex];

		if (Output->CurrOutputIndex == 4900)
		{
			int x = 0;
		}

		// check if the held pattern is already in known patterns.
		bool32 PatternFound = false;
		for (uint64 PatternIndex = 0; PatternIndex < PatternNextIndex; PatternIndex++)
		{
			known_pattern* NextPattern = &KnownPatterns[PatternIndex];
			if (NextPattern->CodePatternLength == (uint32)(IndexBufferNext + 1))
			{
				bool32 ValidPattern = true;
				for (uint64 PatIndex = 0; PatIndex < NextPattern->CodePatternLength - 1; PatIndex++)
				{
					if (NextPattern->CodePattern[PatIndex] != IndexBuffer[PatIndex])
					{
						ValidPattern = false;
					}
				}
				if (NextPattern->CodePattern[NextPattern->CodePatternLength - 1] != NextCode)
				{
					ValidPattern = false;
				}

				if (ValidPattern)
				{
					PatternFound = true;
					IndexBuffer[IndexBufferNext] = NextCode;
					IndexBufferNext++;
					Assert(IndexBufferNext < IndexBufferIndexSize);
					break;
				}
			}
		}

		// Pattern not found: add the held pattern into the known patterns, and add index buffer into the code stream
		if (!PatternFound)
		{
			// Add to the code stream. If pattern is found, use that index, else, just add the index buffer itself
			bool32 MatchingFound = false;
			for (uint16 PatternIndex = 0; PatternIndex < PatternNextIndex; PatternIndex++)
			{
				known_pattern* NextPattern = &KnownPatterns[PatternIndex];
				if (NextPattern->CodePatternLength == (uint32)(IndexBufferNext))
				{
					bool32 ValidPattern = true;
					for (uint64 PatIndex = 0; PatIndex < NextPattern->CodePatternLength; PatIndex++)
					{
						if (NextPattern->CodePattern[PatIndex] != IndexBuffer[PatIndex])
						{
							ValidPattern = false;
						}
					}
					if (ValidPattern)
					{
						MatchingFound = true;
						Assert(PatternIndex < PatternNextIndex);
						AddPatternToOutput(Output, PatternIndex);
						break;
					}
				}
			}
			Assert(MatchingFound);

			// add the new pattern
			if (PatternNextIndex == Pow(2, Output->CurrBitSize))
			{
				Output->CurrBitSize++;
			}

			known_pattern* NextPattern = &KnownPatterns[PatternNextIndex];
			PatternNextIndex++;

			for (uint32 HeldIndex = 0; HeldIndex < IndexBufferNext; HeldIndex++)
			{
				// add the new code into the known patterns
				NextPattern->CodePattern[HeldIndex] = IndexBuffer[HeldIndex];
			}
			NextPattern->CodePattern[IndexBufferNext] = NextCode;
			NextPattern->CodePatternLength = (uint32)(IndexBufferNext + 1);
			Assert(NextPattern->CodePatternLength < CodePattern_Max);

			IndexBufferNext = 1;
			IndexBuffer[0] = NextCode;

			// Hit pattern limit, reset the pattern table
			if (PatternNextIndex >= Max_Patterns - 2)
			{
				AddPatternToOutput(Output, ClearCode);
				PatternNextIndex = (uint16)(ColTableSize + 2);

				Output->CurrBitSize = CurrFrame->MinCodeSize + 1;
			}

			Assert(PatternNextIndex < Max_Patterns);
		}
	}

	// Output what is left over in the IndexBuffer
	for (uint16 PatternIndex = 0; PatternIndex < PatternNextIndex; PatternIndex++)
	{
		known_pattern* NextPattern = &KnownPatterns[PatternIndex];
		if (NextPattern->CodePatternLength == (uint32)(IndexBufferNext))
		{
			bool32 ValidPattern = true;
			for (uint32 PatIndex = 0; PatIndex < NextPattern->CodePatternLength; PatIndex++)
			{
				if (NextPattern->CodePattern[PatIndex] != IndexBuffer[PatIndex])
				{
					ValidPattern = false;
				}
			}
			if (ValidPattern)
			{
				AddPatternToOutput(Output, PatternIndex);
				break;
			}
		}
	}

	AddPatternToOutput(Output, EOICode);
}

void
CopyData(uint8* Dest, uint8* Source, uint32 SourceByteLength)
{
	for (uint32 Index = 0; Index < SourceByteLength; Index++)
	{
		Dest[Index] = Source[Index];
	}
}

void
AddCodeFrame(gif* Gif, uint8* ImageIndecies, uint8 FrameDelay, bool32 UsingLocalColorTable, memory_arena* TransientMem)
{
	frame* NextFrame = &Gif->Frames[Gif->NextFrame];
	NextFrame->CompressedImageData = {};

	// Initialize
	NextFrame->ImageDesc = {};
	NextFrame->GraphicsControlExtension = {};

	// These don't ever change
	NextFrame->ImageDesc.ImageSeperator = 0x2C;
	NextFrame->GraphicsControlExtension.Introducer = 0x21;
	NextFrame->GraphicsControlExtension.ControlLabel = 0xF9;
	NextFrame->GraphicsControlExtension.ByteSize = 0x04;
	NextFrame->GraphicsControlExtension.BlockTerminator = 0;

	// Can change these
	NextFrame->ImageDesc.ImageLeft = 0;
	NextFrame->ImageDesc.ImageTop = 0;
	NextFrame->ImageDesc.ImageWidth = Gif->Width;
	NextFrame->ImageDesc.ImageHeight = Gif->Height;
	if (!UsingLocalColorTable)
	{
		NextFrame->ImageDesc.PackedField = 0;
	}
	else
	{
		uint8 LocalTableSize = 0;
		for (uint8 Index = 0; Index < 8; Index++)
		{
			if (Pow(2, (Index + 1)) >= NextFrame->NextColTableIndex)
			{
				LocalTableSize = Index;
				break;
			}
		}

		NextFrame->MinCodeSize = LocalTableSize + 1;
		Assert(NextFrame->MinCodeSize <= 255);

		NextFrame->ImageDesc.PackedField = 0x80 | LocalTableSize;
	}

	NextFrame->GraphicsControlExtension.PackedField = ((uint8)Gif->FrameDisposalMethod << 2);
	NextFrame->GraphicsControlExtension.DelayTime = FrameDelay;
	NextFrame->GraphicsControlExtension.TransparentColorIndex = 0;

	uint32 PixelCount = Gif->Width * Gif->Height;
	CompressImageData(&NextFrame->CompressedImageData, Gif, ImageIndecies, PixelCount, TransientMem);
}

// Usually frames are added together one right after another, call this when starting that process.
// This allocates memory which is used when compressing the frames. Each frame uses the same memory and clears it at start.
void
StartAddingFrames(gif* Gif, memory_arena* Mem)
{
	Gif->KnownPatterns = (known_pattern*)ArenaAllocate(Mem, sizeof(known_pattern) * Max_Patterns);
	ClearMemory((uint8*)Gif->KnownPatterns, sizeof(known_pattern) * Max_Patterns);
}

/*
This takes pixels in starting at the bottom left and moving right. Input values between 0 and 255 It builds codes adding the colors to the local color table.
Then lZW compresses those codes filling the necessary structs.
The AllowedDistance param is effetively a quality value. Lower value means more quality,
	0 meaning a new color table index is only added if an exact color match is found.
	Higher values allow for higher distances. (Distance == Euclidean distance)
*/
void
AddImageFrame(gif* Gif, uint8* ImageData, real64 AllowedDistance, uint8 FrameDelay, memory_arena* TransientMem)
{
	frame* CurrFrame = &Gif->Frames[Gif->NextFrame];
	CurrFrame->UsingLocalColorTable = true;
	CurrFrame->NextColTableIndex = 0;

	int32 CodesCount = Gif->Width * Gif->Height;
	uint8* CodeStream = (uint8*)ArenaAllocate(TransientMem, CodesCount);
	ClearMemory(CodeStream, CodesCount);
	uint32 CodeStreamIndex = 0;

	int64 TotalData = CodesCount * 3;
	int64 RowSize = Gif->Width * 3;
	int64 NextPixelIndex = TotalData - RowSize;

	while (NextPixelIndex >= 0)
	{
		color NewPixelColor = {};
		NewPixelColor.R = ImageData[NextPixelIndex] / 255.0f;
		NewPixelColor.G = ImageData[NextPixelIndex + 1] / 255.0f;
		NewPixelColor.B = ImageData[NextPixelIndex + 2] / 255.0f;
		NewPixelColor.A = 0.0f;

		NextPixelIndex += 3;
		if (NextPixelIndex % RowSize == 0)
		{
			NextPixelIndex -= RowSize * 2;
		}

		//Go through local color table
		//	If found a similar color then use that color for the next code
		// 	If not found a similar color, then put that color into the local color table and use the new index for the next code

		bool32 FoundColor = false;
		for (uint8 TableIndex = 0; TableIndex < CurrFrame->NextColTableIndex; TableIndex++)
		{
			color TableColor = CurrFrame->LocalColorTable[TableIndex];

			real64 RedSquared = Square(NewPixelColor.R - TableColor.R);
			real64 GreenSquared = Square(NewPixelColor.G - TableColor.G);
			real64 BlueSquared = Square(NewPixelColor.B - TableColor.B);
			real64 ColorDistance = SquareRoot(RedSquared + GreenSquared + BlueSquared);

			if (ColorDistance < AllowedDistance)
			{
				FoundColor = true;
				CodeStream[CodeStreamIndex] = TableIndex;
				CodeStreamIndex++;
				break;
			}
		}

		// Add that color into the table
		if (!FoundColor)
		{
			CurrFrame->LocalColorTable[CurrFrame->NextColTableIndex] = NewPixelColor;
			CodeStream[CodeStreamIndex] = (uint8)CurrFrame->NextColTableIndex;

			CodeStreamIndex++;
			CurrFrame->NextColTableIndex++;

			// Local color table cannot exceed 255
			Assert(CurrFrame->NextColTableIndex < 255);
		}
	}

	AddCodeFrame(Gif, CodeStream, FrameDelay, true, TransientMem);

	Gif->NextFrame++;
	Assert(Gif->NextFrame < 100);
}

void
SaveGif(gif* Gif, string SaveLoc,
        memory_arena* TransientMem,
        platform_write_file PlatformWriteFile)
{
	gif_header GifHeader = {};

	GifHeader.CanvasWidth = Gif->Width;
	GifHeader.CanvasHeight = Gif->Height;

	// This is a default of (using color table)1 (2 bit color resolution)001 (not sorted)0 (color table size of 4)001
	GifHeader.PackField = 0x91;

	GifHeader.BackgroundColorIndex = 3;
	GifHeader.PixelAspectRatio = 0;

	GifHeader.GlobalColorTable[0] = color_table_index{0xFF, 0xFF, 0xFF}; // black
	GifHeader.GlobalColorTable[1] = color_table_index{0xFF, 0x00, 0x00}; // red
	GifHeader.GlobalColorTable[2] = color_table_index{0x00, 0x00, 0xFF}; // blue
	GifHeader.GlobalColorTable[3] = color_table_index{0x00, 0x00, 0x00}; // white

	/*
	Pack everything together

	Order
		Header

		Graphics Control Extension
		Image Descriptor
		Image Data
		(repeat above 3 for each frame)

		Trailer
	*/

	uint8 MaxBytesPerBlock = 255;

	// Accumulate the total file size
	uint32 TotalFileSize = 0;
	TotalFileSize += sizeof(gif_header);

	uint32 FrameCount = Gif->NextFrame;

	for (uint32 FrameIndex = 0; FrameIndex < FrameCount; FrameIndex++)
	{
		frame* CurrFrame = &Gif->Frames[FrameIndex];

		TotalFileSize += sizeof(graphics_control_extension);
		TotalFileSize += sizeof(image_descriptor);

		// Add color table size
		if (CurrFrame->UsingLocalColorTable)
		{
			uint8 FieldVal = CurrFrame->ImageDesc.PackedField & 0x07;
			TotalFileSize += (uint8)(Pow(2, FieldVal + 1) * 3);
		}

		// For the LZW min code size
		TotalFileSize++;

		uint32 FrameBytesCount = (CurrFrame->CompressedImageData.CurrOutputIndex * 2);
		TotalFileSize += FrameBytesCount;
		TotalFileSize += (FrameBytesCount / MaxBytesPerBlock) + 1;

		TotalFileSize++; // For the block terminator
	}

	// For the trailer
	TotalFileSize++;

	// TODO remove this. This is so lazy. I'm missing some piece of data in the size accumulation.
	TotalFileSize += 2000;

	uint8* GifMem = (uint8*)ArenaAllocate(TransientMem, TotalFileSize);
	ClearMemory(GifMem, TotalFileSize);

	void* FileHead = (void*)GifMem;

	// Place the header
	CopyData(GifMem, (uint8*)&GifHeader, sizeof(gif_header));
	GifMem += sizeof(gif_header);

	for (uint32 FrameIndex = 0; FrameIndex < FrameCount; FrameIndex++)
	{
		frame* CurrFrame = &Gif->Frames[FrameIndex];

		// CurrOutputIndex must be atleast one
		if (CurrFrame->CompressedImageData.CurrOutputIndex == 0)
		{
			CurrFrame->CompressedImageData.CurrOutputIndex = 1;
		}

		// Place the Graphics Control Extension
		CopyData(GifMem, (uint8*)&CurrFrame->GraphicsControlExtension, sizeof(graphics_control_extension));
		GifMem += sizeof(graphics_control_extension);

		// Place the image desc
		CopyData(GifMem, (uint8*)&CurrFrame->ImageDesc, sizeof(image_descriptor));
		GifMem += sizeof(image_descriptor);

		// Place Local color table
		if (CurrFrame->UsingLocalColorTable)
		{
			uint8 FieldVal = CurrFrame->ImageDesc.PackedField & 0x07;
			uint8 ColTableSize = (uint8)Pow(2, FieldVal + 1);

			for (uint8 Index = 0; Index < ColTableSize; Index++)
			{
				color Col = CurrFrame->LocalColorTable[Index];
				GifMem[0] = (uint8)(Col.R * 255);
				GifMem[1] = (uint8)(Col.G * 255);
				GifMem[2] = (uint8)(Col.B * 255);
				GifMem += 3;
			}
		}

		// Add LZW min code size
		GifMem[0] = CurrFrame->MinCodeSize;
		GifMem++;

		uint32 CurrBytesCount = (CurrFrame->CompressedImageData.CurrOutputIndex * 2);

		// Put the data into 8 bits
		uint8* DataInEight = (uint8*)ArenaAllocate(TransientMem, CurrBytesCount);
		ClearMemory(DataInEight, CurrBytesCount);

		uint8* NextEight = DataInEight;
		for (uint64 Index = 0; Index < CurrFrame->CompressedImageData.CurrOutputIndex; Index++)
		{
			NextEight[0] = (uint8)CurrFrame->CompressedImageData.Output[Index];
			NextEight++;
			NextEight[0] = (uint8)(CurrFrame->CompressedImageData.Output[Index] >> 8);
			NextEight++;
		}

		// Place the image output
		for (uint64 Index = 0; Index < CurrBytesCount; Index++)
		{
			if (Index % MaxBytesPerBlock == 0)
			{
				GifMem[0] = (uint8)Min((uint64)MaxBytesPerBlock, (uint64)(CurrBytesCount - Index));
				GifMem++;
			}

			GifMem[0] = (uint8)DataInEight[Index];
			GifMem++;
		}

		// Place end of block terminator
		GifMem[0] = 0x00;
		GifMem++;
	}

	// Place the trailer, it's always 0x3B
	GifMem[0] = 0x3B;
	GifMem++;

	uint64 SizeUsed = GifMem - (uint8*)FileHead;
	Assert(SizeUsed <= TotalFileSize);

	PlatformWriteFile(SaveLoc.CharArray, FileHead, TotalFileSize);
}