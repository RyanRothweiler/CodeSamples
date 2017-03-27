using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.IO;
// using UnityEditor;

public enum ControllerType
{
	// NOTE these controllers must be at top of enum and in order
	Controller0,
	Controller1,
	Controller2,
	Controller3,
	Controller4,
	Controller5,
	// Controller6,

	KeyboardWASD,
	KeyboardArrows,

	None,
	Count,
};

public struct Key
{
	/*
	NOTE either the boundKey or the boundAxis is used. Not both.
	Set the boundKey = KeyCode.None if it's not being used, otherwise, it will be assumed to be used
	Axis direction controls if the bound axis is checked in the positive or negative direction, (<0 for negative >0 for positive)
	*/
	public Key(KeyCode keyCode)
	{
		this.keyCode = keyCode;
		this.axisName = "NONE";
		this.axisDirection = 0;
		this.isValid = true;
	}

	public Key(string axisName, int axisDirection)
	{
		this.keyCode = KeyCode.None;
		this.axisName = axisName;
		this.axisDirection = axisDirection;
		this.isValid = true;
	}

	public KeyCode keyCode;
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)] public string axisName;
	public int axisDirection;

	// this is used to show that this key does not representa valid key
	public bool isValid;
};

public struct InputState
{
	public bool isDown;
	public bool onDown;
	public bool isUp;
	public bool onUp;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)] public string buttonName;

	public Key boundKey;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Controls
{
	// NOTE the input states need to be up at the top
	public InputState jump;
	public InputState attack;

	public InputState moveUp;
	public InputState moveDown;

	public InputState moveLeft;
	public InputState moveRight;

	public InputState escape;

	public float xMovement;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AllControllers
{
	// public Controls[] controllers;
	public Controls contollerOne;
	public Controls contollerTwo;
	public Controls contollerThree;
	public Controls contollerFour;
	public Controls contollerFive;
	public Controls contollerSix;
	public Controls contollerSeven;
};

public enum ReplayState
{
	Recording, Playing, None,
};

public class Controllers : MonoBehaviour
{

	// if we already have a Controller object
	private static bool created = false;
	public static Controllers instance;

	public static Controls[] allControllers = new Controls[(int)ControllerType.Count];

	public ReplayState replayState;
	// NOTE this is about two hours of gameplay at 100 fps
	public static AllControllers[] controllerHistory = new AllControllers[7200];
	public string replayPlayingLocation;

	// threshold which to register a stick move
	private static float stickSensitivity = 0.8f;

	// NOTE binding code is dependeng on naming convention of ControllerNUM (example Controller1 or Controller2 )
	public static string[] allAxis =
	{
		"Controller1 Axis 1 X",
		"Controller2 Axis 1 X",
		"Controller3 Axis 1 X",
		"Controller4 Axis 1 X",
		"Controller5 Axis 1 X",
		"Controller6 Axis 1 X",

		"Controller1 Axis 1 Y",
		"Controller2 Axis 1 Y",
		"Controller3 Axis 1 Y",
		"Controller4 Axis 1 Y",
		"Controller5 Axis 1 Y",
		"Controller6 Axis 1 Y",

		// NOTE the above axis need to be in that order. The below order doesn't matter.


		"Controller1 Axis 2 X",
		"Controller2 Axis 2 X",
		"Controller3 Axis 2 X",
		"Controller4 Axis 2 X",
		"Controller5 Axis 2 X",
		"Controller6 Axis 2 X",

		"Controller1 Axis 2 Y",
		"Controller2 Axis 2 Y",
		"Controller3 Axis 2 Y",
		"Controller4 Axis 2 Y",
		"Controller5 Axis 2 Y",
		"Controller6 Axis 2 Y",

		"Controller1 Axis 3 X",
		"Controller2 Axis 3 X",
		"Controller3 Axis 3 X",
		"Controller4 Axis 3 X",
		"Controller5 Axis 3 X",
		"Controller6 Axis 3 X",

		"Controller1 Axis 3 Y",
		"Controller2 Axis 3 Y",
		"Controller3 Axis 3 Y",
		"Controller4 Axis 3 Y",
		"Controller5 Axis 3 Y",
		"Controller6 Axis 3 Y",

		"Controller1 Axis 4 Y",
		"Controller2 Axis 4 Y",
		"Controller3 Axis 4 Y",
		"Controller4 Axis 4 Y",
		"Controller5 Axis 4 Y",
		"Controller6 Axis 4 Y",

		"Controller1 Axis 5 X",
		"Controller2 Axis 5 X",
		"Controller3 Axis 5 X",
		"Controller4 Axis 5 X",
		"Controller5 Axis 5 X",
		"Controller6 Axis 5 X",
	};

	// This is used in cusctom controller bindings
	public static int buttonsPerPad = 20;

	public static KeyCode[] controllerKeyCodes =
	{
		KeyCode.Joystick1Button0,
		KeyCode.Joystick1Button1,
		KeyCode.Joystick1Button2,
		KeyCode.Joystick1Button3,
		KeyCode.Joystick1Button4,
		KeyCode.Joystick1Button5,
		KeyCode.Joystick1Button6,
		KeyCode.Joystick1Button8,
		KeyCode.Joystick1Button9,
		KeyCode.Joystick1Button10,
		KeyCode.Joystick1Button11,
		KeyCode.Joystick1Button12,
		KeyCode.Joystick1Button13,
		KeyCode.Joystick1Button14,
		KeyCode.Joystick1Button15,
		KeyCode.Joystick1Button16,
		KeyCode.Joystick1Button17,
		KeyCode.Joystick1Button18,
		KeyCode.Joystick1Button19,

		KeyCode.Joystick2Button0,
		KeyCode.Joystick2Button1,
		KeyCode.Joystick2Button2,
		KeyCode.Joystick2Button3,
		KeyCode.Joystick2Button4,
		KeyCode.Joystick2Button5,
		KeyCode.Joystick2Button6,
		KeyCode.Joystick2Button8,
		KeyCode.Joystick2Button9,
		KeyCode.Joystick2Button10,
		KeyCode.Joystick2Button11,
		KeyCode.Joystick2Button12,
		KeyCode.Joystick2Button13,
		KeyCode.Joystick2Button14,
		KeyCode.Joystick2Button15,
		KeyCode.Joystick2Button16,
		KeyCode.Joystick2Button17,
		KeyCode.Joystick2Button18,
		KeyCode.Joystick2Button19,

		KeyCode.Joystick3Button0,
		KeyCode.Joystick3Button1,
		KeyCode.Joystick3Button2,
		KeyCode.Joystick3Button3,
		KeyCode.Joystick3Button4,
		KeyCode.Joystick3Button5,
		KeyCode.Joystick3Button6,
		KeyCode.Joystick3Button8,
		KeyCode.Joystick3Button9,
		KeyCode.Joystick3Button10,
		KeyCode.Joystick3Button11,
		KeyCode.Joystick3Button12,
		KeyCode.Joystick3Button13,
		KeyCode.Joystick3Button14,
		KeyCode.Joystick3Button15,
		KeyCode.Joystick3Button16,
		KeyCode.Joystick3Button17,
		KeyCode.Joystick3Button18,
		KeyCode.Joystick3Button19,

		KeyCode.Joystick4Button0,
		KeyCode.Joystick4Button1,
		KeyCode.Joystick4Button2,
		KeyCode.Joystick4Button3,
		KeyCode.Joystick4Button4,
		KeyCode.Joystick4Button5,
		KeyCode.Joystick4Button6,
		KeyCode.Joystick4Button8,
		KeyCode.Joystick4Button9,
		KeyCode.Joystick4Button10,
		KeyCode.Joystick4Button11,
		KeyCode.Joystick4Button12,
		KeyCode.Joystick4Button13,
		KeyCode.Joystick4Button14,
		KeyCode.Joystick4Button15,
		KeyCode.Joystick4Button16,
		KeyCode.Joystick4Button17,
		KeyCode.Joystick4Button18,
		KeyCode.Joystick4Button19,

		KeyCode.Joystick5Button0,
		KeyCode.Joystick5Button1,
		KeyCode.Joystick5Button2,
		KeyCode.Joystick5Button3,
		KeyCode.Joystick5Button4,
		KeyCode.Joystick5Button5,
		KeyCode.Joystick5Button6,
		KeyCode.Joystick5Button8,
		KeyCode.Joystick5Button9,
		KeyCode.Joystick5Button10,
		KeyCode.Joystick5Button11,
		KeyCode.Joystick5Button12,
		KeyCode.Joystick5Button13,
		KeyCode.Joystick5Button14,
		KeyCode.Joystick5Button15,
		KeyCode.Joystick5Button16,
		KeyCode.Joystick5Button17,
		KeyCode.Joystick5Button18,
		KeyCode.Joystick5Button19,

		KeyCode.Joystick6Button0,
		KeyCode.Joystick6Button1,
		KeyCode.Joystick6Button2,
		KeyCode.Joystick6Button3,
		KeyCode.Joystick6Button4,
		KeyCode.Joystick6Button5,
		KeyCode.Joystick6Button6,
		KeyCode.Joystick6Button8,
		KeyCode.Joystick6Button9,
		KeyCode.Joystick6Button10,
		KeyCode.Joystick6Button11,
		KeyCode.Joystick6Button12,
		KeyCode.Joystick6Button13,
		KeyCode.Joystick6Button14,
		KeyCode.Joystick6Button15,
		KeyCode.Joystick6Button16,
		KeyCode.Joystick6Button17,
		KeyCode.Joystick6Button18,
		KeyCode.Joystick6Button19,

		KeyCode.Joystick7Button0,
		KeyCode.Joystick7Button1,
		KeyCode.Joystick7Button2,
		KeyCode.Joystick7Button3,
		KeyCode.Joystick7Button4,
		KeyCode.Joystick7Button5,
		KeyCode.Joystick7Button6,
		KeyCode.Joystick7Button8,
		KeyCode.Joystick7Button9,
		KeyCode.Joystick7Button10,
		KeyCode.Joystick7Button11,
		KeyCode.Joystick7Button12,
		KeyCode.Joystick7Button13,
		KeyCode.Joystick7Button14,
		KeyCode.Joystick7Button15,
		KeyCode.Joystick7Button16,
		KeyCode.Joystick7Button17,
		KeyCode.Joystick7Button18,
		KeyCode.Joystick7Button19,

		KeyCode.Joystick8Button0,
		KeyCode.Joystick8Button1,
		KeyCode.Joystick8Button2,
		KeyCode.Joystick8Button3,
		KeyCode.Joystick8Button4,
		KeyCode.Joystick8Button5,
		KeyCode.Joystick8Button6,
		KeyCode.Joystick8Button8,
		KeyCode.Joystick8Button9,
		KeyCode.Joystick8Button10,
		KeyCode.Joystick8Button11,
		KeyCode.Joystick8Button12,
		KeyCode.Joystick8Button13,
		KeyCode.Joystick8Button14,
		KeyCode.Joystick8Button15,
		KeyCode.Joystick8Button16,
		KeyCode.Joystick8Button17,
		KeyCode.Joystick8Button18,
		KeyCode.Joystick8Button19,
	};
	public static KeyCode[] keyboardKeyCodes =
	{
		KeyCode.Backspace,
		KeyCode.Delete,
		KeyCode.Tab,
		KeyCode.Clear,
		KeyCode.Return,
		KeyCode.Pause,
		KeyCode.Escape,
		KeyCode.Space,
		KeyCode.Keypad0,
		KeyCode.Keypad1,
		KeyCode.Keypad2,
		KeyCode.Keypad3,
		KeyCode.Keypad4,
		KeyCode.Keypad5,
		KeyCode.Keypad6,
		KeyCode.Keypad7,
		KeyCode.Keypad8,
		KeyCode.Keypad9,
		KeyCode.KeypadPeriod,
		KeyCode.KeypadDivide,
		KeyCode.KeypadMultiply,
		KeyCode.KeypadMinus,
		KeyCode.KeypadPlus,
		KeyCode.KeypadEnter,
		KeyCode.KeypadEquals,
		KeyCode.UpArrow,
		KeyCode.DownArrow,
		KeyCode.RightArrow,
		KeyCode.LeftArrow,
		KeyCode.Insert,
		KeyCode.Home,
		KeyCode.End,
		KeyCode.PageUp,
		KeyCode.PageDown,
		KeyCode.F1,
		KeyCode.F2,
		KeyCode.F3,
		KeyCode.F4,
		KeyCode.F5,
		KeyCode.F6,
		KeyCode.F7,
		KeyCode.F8,
		KeyCode.F9,
		KeyCode.F10,
		KeyCode.F11,
		KeyCode.F12,
		KeyCode.F13,
		KeyCode.F14,
		KeyCode.F15,
		KeyCode.Alpha0,
		KeyCode.Alpha1,
		KeyCode.Alpha2,
		KeyCode.Alpha3,
		KeyCode.Alpha4,
		KeyCode.Alpha5,
		KeyCode.Alpha6,
		KeyCode.Alpha7,
		KeyCode.Alpha8,
		KeyCode.Alpha9,
		KeyCode.Exclaim,
		KeyCode.DoubleQuote,
		KeyCode.Hash,
		KeyCode.Dollar,
		KeyCode.Ampersand,
		KeyCode.Quote,
		KeyCode.LeftParen,
		KeyCode.RightParen,
		KeyCode.Asterisk,
		KeyCode.Plus,
		KeyCode.Comma,
		KeyCode.Minus,
		KeyCode.Period,
		KeyCode.Slash,
		KeyCode.Colon,
		KeyCode.Semicolon,
		KeyCode.Less,
		KeyCode.Equals,
		KeyCode.Greater,
		KeyCode.Question,
		KeyCode.At,
		KeyCode.LeftBracket,
		KeyCode.Backslash,
		KeyCode.RightBracket,
		KeyCode.Caret,
		KeyCode.Underscore,
		KeyCode.BackQuote,
		KeyCode.A,
		KeyCode.B,
		KeyCode.C,
		KeyCode.D,
		KeyCode.E,
		KeyCode.F,
		KeyCode.G,
		KeyCode.H,
		KeyCode.I,
		KeyCode.J,
		KeyCode.K,
		KeyCode.L,
		KeyCode.M,
		KeyCode.N,
		KeyCode.O,
		KeyCode.P,
		KeyCode.Q,
		KeyCode.R,
		KeyCode.S,
		KeyCode.T,
		KeyCode.U,
		KeyCode.V,
		KeyCode.W,
		KeyCode.X,
		KeyCode.Y,
		KeyCode.Z,
		KeyCode.Numlock,
		KeyCode.CapsLock,
		KeyCode.ScrollLock,
		KeyCode.RightShift,
		KeyCode.LeftShift,
		KeyCode.RightControl,
		KeyCode.LeftControl,
		KeyCode.RightAlt,
		KeyCode.LeftAlt,
		KeyCode.LeftCommand,
		KeyCode.LeftApple,
		KeyCode.LeftWindows,
		KeyCode.RightCommand,
		KeyCode.RightApple,
		KeyCode.RightWindows,
		KeyCode.AltGr,
		KeyCode.Help,
		KeyCode.Print,
		KeyCode.SysReq,
		KeyCode.Break,
		KeyCode.Menu,
		// KeyCode.Mouse0,
		// KeyCode.Mouse1,
		// KeyCode.Mouse2,
		// KeyCode.Mouse3,
		// KeyCode.Mouse4,
		// KeyCode.Mouse5,
		// KeyCode.Mouse6,
	};


	public void Start()
	{
		if (!created)
		{
			instance = this;
			created = true;
			DontDestroyOnLoad(this.gameObject);

			SetDefaultBindings();

			if (PlayerPrefs.HasKey("ControllerDataExists"))
			{
				Debug.Log("LOADING controller settings");

				LoadController(ControllerType.KeyboardWASD);
				LoadController(ControllerType.KeyboardArrows);
				LoadController(ControllerType.Controller0);
				LoadController(ControllerType.Controller1);
				LoadController(ControllerType.Controller2);
				LoadController(ControllerType.Controller3);
				LoadController(ControllerType.Controller4);
				LoadController(ControllerType.Controller5);

			}
			else
			{
				Debug.Log("SETTING controller settings");
				SaveAllControllers();
			}
		}
		else
		{
			Destroy(this.gameObject);
		}


		if (replayState == ReplayState.Playing)
		{
			byte[] fileBytes = File.ReadAllBytes(replayPlayingLocation);

			int oneFrameSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AllControllers));
			for (int frameIndex = 0; frameIndex < Controllers.controllerHistory.Length; frameIndex++)
			{
				int byteOffset = frameIndex * oneFrameSize;
				byte[] oneFrameBytes = new byte[oneFrameSize];

				for (int byteIndex = 0; byteIndex < oneFrameSize; byteIndex++)
				{
					oneFrameBytes[byteIndex] = fileBytes[byteOffset + byteIndex];
				}

				Controllers.controllerHistory[frameIndex] = DeserializeMsg<AllControllers>(oneFrameBytes);

				// if (Controllers.controllerHistory[frameIndex].contollerOne.jump.onDown)
				// {
				// 	Debug.Log("jump down");
				// }

			}

			Debug.Log("Successfully Loaded Replay");
		}

	}

	public static void SetDefaultBindings()
	{
		// jump, attack, pause, move up, move down, move right, move left
		BindController(ControllerType.KeyboardWASD,
		               new Key(KeyCode.E), new Key(KeyCode.Q), new Key(KeyCode.Escape),
		               new Key(KeyCode.W), new Key(KeyCode.S), new Key(KeyCode.D), new Key(KeyCode.A));

		BindController(ControllerType.KeyboardArrows,
		               new Key(KeyCode.RightControl), new Key(KeyCode.RightShift), new Key(KeyCode.End),
		               new Key(KeyCode.UpArrow), new Key(KeyCode.DownArrow), new Key(KeyCode.RightArrow), new Key(KeyCode.LeftArrow));

		BindController(ControllerType.Controller0,
		               new Key(KeyCode.Joystick1Button0), new Key(KeyCode.Joystick1Button2), new Key(KeyCode.Joystick1Button7),
		               new Key(allAxis[6], -1), new Key(allAxis[6], 1), new Key(allAxis[0], 1), new Key(allAxis[0], -1));

		BindController(ControllerType.Controller1,
		               new Key(KeyCode.Joystick2Button0), new Key(KeyCode.Joystick2Button2), new Key(KeyCode.Joystick2Button7),
		               new Key(allAxis[7], -1), new Key(allAxis[7], 1), new Key(allAxis[1], 1), new Key(allAxis[1], -1));

		BindController(ControllerType.Controller2,
		               new Key(KeyCode.Joystick3Button0), new Key(KeyCode.Joystick3Button2), new Key(KeyCode.Joystick3Button7),
		               new Key(allAxis[8], -1), new Key(allAxis[8], 1), new Key(allAxis[2], 1), new Key(allAxis[2], -1));

		BindController(ControllerType.Controller3,
		               new Key(KeyCode.Joystick4Button0), new Key(KeyCode.Joystick4Button2), new Key(KeyCode.Joystick4Button7),
		               new Key(allAxis[9], -1), new Key(allAxis[9], 1), new Key(allAxis[3], 1), new Key(allAxis[3], -1));

		BindController(ControllerType.Controller4,
		               new Key(KeyCode.Joystick5Button0), new Key(KeyCode.Joystick5Button2), new Key(KeyCode.Joystick5Button7),
		               new Key(allAxis[10], -1), new Key(allAxis[10], 1), new Key(allAxis[4], 1), new Key(allAxis[4], -1));

		BindController(ControllerType.Controller5,
		               new Key(KeyCode.Joystick6Button0), new Key(KeyCode.Joystick6Button2), new Key(KeyCode.Joystick6Button7),
		               new Key(allAxis[11], -1), new Key(allAxis[11], 1), new Key(allAxis[5], 1), new Key(allAxis[5], -1));
	}

	public static void SaveAllControllers()
	{
		SaveController(ControllerType.KeyboardWASD);
		SaveController(ControllerType.KeyboardArrows);
		SaveController(ControllerType.Controller0);
		SaveController(ControllerType.Controller1);
		SaveController(ControllerType.Controller2);
		SaveController(ControllerType.Controller3);
		SaveController(ControllerType.Controller4);
		SaveController(ControllerType.Controller5);
		PlayerPrefs.SetInt("ControllerDataExists", 1);
	}

	public static void LoadController(ControllerType typeLoading)
	{
		LoadInputState(ref allControllers[(int)typeLoading].jump, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].attack, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].escape, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].moveUp, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].moveDown, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].moveRight, typeLoading);
		LoadInputState(ref allControllers[(int)typeLoading].moveLeft, typeLoading);
	}

	public static void LoadInputState(ref InputState stateLoadingInto, ControllerType controllerType)
	{
		stateLoadingInto.boundKey.keyCode = (KeyCode)PlayerPrefs.GetInt((int)controllerType + stateLoadingInto.buttonName + "key keyCode");
		stateLoadingInto.boundKey.axisName = PlayerPrefs.GetString((int)controllerType + stateLoadingInto.buttonName + "key axisName");
		stateLoadingInto.boundKey.axisDirection = PlayerPrefs.GetInt((int)controllerType + stateLoadingInto.buttonName + "key axisDirection");

		int isValid = PlayerPrefs.GetInt((int)controllerType + "key isValid");
		if (isValid == 1)
		{
			stateLoadingInto.boundKey.isValid = true;
		}
		else
		{
			stateLoadingInto.boundKey.isValid = false;
		}
	}

	public static void SaveController(ControllerType typeSaving)
	{
		Controls controlsSaving = allControllers[(int)typeSaving];
		SaveInputState(typeSaving, controlsSaving.jump);
		SaveInputState(typeSaving, controlsSaving.attack);
		SaveInputState(typeSaving, controlsSaving.escape);
		SaveInputState(typeSaving, controlsSaving.moveUp);
		SaveInputState(typeSaving, controlsSaving.moveDown);
		SaveInputState(typeSaving, controlsSaving.moveRight);
		SaveInputState(typeSaving, controlsSaving.moveLeft);
	}

	public static void SaveInputState(ControllerType controllerType, InputState stateSaving)
	{
		// PlayerPrefs.SetString((int)controllerType + "buttonName", stateSaving.buttonName);

		PlayerPrefs.SetInt((int)controllerType + stateSaving.buttonName + "key keyCode", (int)stateSaving.boundKey.keyCode);
		PlayerPrefs.SetString((int)controllerType + stateSaving.buttonName + "key axisName", stateSaving.boundKey.axisName);
		PlayerPrefs.SetInt((int)controllerType + stateSaving.buttonName + "key axisDirection", (int)stateSaving.boundKey.axisDirection);

		if (stateSaving.boundKey.isValid)
		{
			PlayerPrefs.SetInt((int)controllerType + "key isValid", 1);
		}
		else
		{
			PlayerPrefs.SetInt((int)controllerType + "key isValid", 0);
		}
	}

	public static void BindController(ControllerType controllerType,
	                                  Key jumpKey, Key attackKey, Key pauseKey,
	                                  Key moveUpKey, Key moveDownKey, Key moveRightKey, Key moveLeftKey)
	{
		InitializeButton(ref allControllers[(int)controllerType].jump, "JUMP", jumpKey);
		InitializeButton(ref allControllers[(int)controllerType].attack, "ATTACK", attackKey);
		InitializeButton(ref allControllers[(int)controllerType].escape, "PAUSE", pauseKey);
		InitializeButton(ref allControllers[(int)controllerType].moveUp, "MOVE UP", moveUpKey);
		InitializeButton(ref allControllers[(int)controllerType].moveDown, "MOVE DOWN", moveDownKey);
		InitializeButton(ref allControllers[(int)controllerType].moveLeft, "MOVE LEFT", moveLeftKey);
		InitializeButton(ref allControllers[(int)controllerType].moveRight, "MOVE RIGHT", moveRightKey);
	}

	public static void InitializeButton(ref InputState stateClearing, string buttonName, Key boundKey)
	{
		stateClearing.isDown = false;
		stateClearing.onDown = false;
		stateClearing.isUp = false;
		stateClearing.onUp = false;
		stateClearing.buttonName = buttonName;
		stateClearing.boundKey = boundKey;
	}

	public void Update ()
	{
		if (replayState != ReplayState.Playing)
		{
			for (int padIndex = 0; padIndex < 8; padIndex++)
			{
				CheckKey(ref allControllers[padIndex].moveUp);
				CheckKey(ref allControllers[padIndex].moveDown);
				CheckKey(ref allControllers[padIndex].moveLeft);
				CheckKey(ref allControllers[padIndex].moveRight);
				CheckKey(ref allControllers[padIndex].jump);
				CheckKey(ref allControllers[padIndex].attack);
				CheckKey(ref allControllers[padIndex].escape);

				allControllers[padIndex].xMovement = 0.0f;
				if (allControllers[padIndex].moveRight.isDown)
				{
					allControllers[padIndex].xMovement = 1.0f;
				}
				if (allControllers[padIndex].moveLeft.isDown)
				{
					allControllers[padIndex].xMovement = -1.0f;
				}
			}
		}

		if (replayState == ReplayState.Recording)
		{
			if (Time.frameCount >= controllerHistory.Length)
			{
				Debug.LogWarning("Max recording limit reached");
				replayState = ReplayState.None;
				SaveReplay();
			}
			else
			{
				controllerHistory[Time.frameCount].contollerOne = allControllers[(int)ControllerType.KeyboardWASD];
				controllerHistory[Time.frameCount].contollerTwo = allControllers[(int)ControllerType.KeyboardArrows];
				controllerHistory[Time.frameCount].contollerThree = allControllers[(int)ControllerType.Controller0];
				controllerHistory[Time.frameCount].contollerFour = allControllers[(int)ControllerType.Controller1];
				controllerHistory[Time.frameCount].contollerFive = allControllers[(int)ControllerType.Controller2];
				controllerHistory[Time.frameCount].contollerSix = allControllers[(int)ControllerType.Controller3];
				controllerHistory[Time.frameCount].contollerSeven = allControllers[(int)ControllerType.Controller4];
			}
		}

		if (replayState == ReplayState.Playing)
		{
			allControllers[(int)ControllerType.KeyboardWASD] = controllerHistory[Time.frameCount].contollerOne;
			allControllers[(int)ControllerType.KeyboardArrows] = controllerHistory[Time.frameCount].contollerTwo;
			allControllers[(int)ControllerType.Controller0] = controllerHistory[Time.frameCount].contollerThree;
			allControllers[(int)ControllerType.Controller1] = controllerHistory[Time.frameCount].contollerFour;
			allControllers[(int)ControllerType.Controller2] = controllerHistory[Time.frameCount].contollerFive;
			allControllers[(int)ControllerType.Controller3] = controllerHistory[Time.frameCount].contollerSix;
			allControllers[(int)ControllerType.Controller4] = controllerHistory[Time.frameCount].contollerSeven;
		}
	}

	public void CheckKey(ref InputState inputState)
	{
		Key keyChecking = inputState.boundKey;
		if (keyChecking.isValid)
		{
			if (keyChecking.keyCode == KeyCode.None)
			{
				if (keyChecking.axisDirection > 0)
				{
					bool newState = Input.GetAxis(keyChecking.axisName) > stickSensitivity;
					UpdateInputState(ref inputState, newState);
				}
				else
				{
					bool newState = Input.GetAxis(keyChecking.axisName) < -stickSensitivity;
					UpdateInputState(ref inputState, newState);
				}
			}
			else
			{
				bool newState = Input.GetKey(keyChecking.keyCode);
				UpdateInputState(ref inputState, newState);
			}
		}
		else
		{
			UpdateInputState(ref inputState, false);
		}
	}

	private void UpdateInputState(ref InputState stateUpdating, bool newState)
	{
		if (newState)
		{
			if (stateUpdating.isDown)
			{
				stateUpdating.onDown = false;
			}
			else
			{
				stateUpdating.isDown = true;
				stateUpdating.onDown = true;
				stateUpdating.isUp = false;
				stateUpdating.onUp = false;
			}
		}
		else
		{
			if (stateUpdating.isUp)
			{
				stateUpdating.onUp = false;
			}
			else
			{
				stateUpdating.isUp = true;
				stateUpdating.onUp = true;
				stateUpdating.isDown = false;
				stateUpdating.onDown = false;
			}
		}
	}

	public static void SaveReplay()
	{
		int oneFrameSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AllControllers));
		byte[] finalArray = new byte[oneFrameSize * Controllers.controllerHistory.Length];

		for (int frameIndex = 0; frameIndex < Controllers.controllerHistory.Length; frameIndex++)
		{
			byte[] historyBytes = SerializeMessage<AllControllers>(Controllers.controllerHistory[frameIndex]);
			int byteOffset = frameIndex * oneFrameSize;
			for (int byteIndex = 0; byteIndex < historyBytes.Length; byteIndex++)
			{
				finalArray[byteIndex + byteOffset] = historyBytes[byteIndex];
			}
		}

		string saveLoc = Application.persistentDataPath + Path.DirectorySeparatorChar + "Replay" + string.Format("{0:HH,mm,ss}", DateTime.Now) + ".ccr";
		File.WriteAllBytes(saveLoc, finalArray);
		Debug.Log("Replay saved to " + saveLoc);
	}

	public static byte[] SerializeMessage<T>(T msg) where T : struct
	{
		int objsize = Marshal.SizeOf(typeof(T));
		byte[] ret = new byte[objsize];
		IntPtr buff = Marshal.AllocHGlobal(objsize);
		Marshal.StructureToPtr(msg, buff, true);
		Marshal.Copy(buff, ret, 0, objsize);
		Marshal.FreeHGlobal(buff);
		return ret;
	}

	public static T DeserializeMsg<T>(byte[] data) where T : struct
	{
		int objsize = Marshal.SizeOf(typeof(T));
		IntPtr buff = Marshal.AllocHGlobal(objsize);
		Marshal.Copy(data, 0, buff, objsize);
		T retStruct = (T)Marshal.PtrToStructure(buff, typeof(T));
		Marshal.FreeHGlobal(buff);
		return retStruct;
	}
}