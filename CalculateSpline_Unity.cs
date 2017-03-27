
// This calculate a constant rate spline path from a given list of positions.
// Written in Unity for a client 

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// This allows this to be ran in the edit mode
[ExecuteInEditMode]
public class Spliner : MonoBehaviour
{
	/* PUBLICS */

	// Holds all the points on this spline which are a linear distance appart
	[HideInInspector] public List<Vector3> linearSplinePoints;
	// Holds the ndoes which make up this spline
	public SplineNode[] allNodes;
	// Holds all the points on which make up the spline line
	[HideInInspector] public List<Vector3> splinePoints;
	// if closing the spline
	public bool closeSpline = false;
	// The color of the spline
	public Color splineColor;


	/* PRIVATES */

	// used to create linear spline points
	private float distanceSinceLast;

	// Called on unity update
	public void Update ()
	{
		Recalculate();

		// Initializes the spline color
		if (splineColor.a == 0)
		{
			splineColor.a = 1;
		}

		// Set each nodes parent
		foreach (SplineNode node in allNodes)
		{
			if (node != null)
			{
				node.parentSpline = this;
			}
		}
	}

	// Recalculates the spline
	public void Recalculate()
	{
		// Recreate the lists, removing all old data
		splinePoints = new List<Vector3>();
		linearSplinePoints = new List<Vector3>();

		// This is used to space the linear spline point apart linearly
		distanceSinceLast = 0;

		// Checks to make sure all the indecies in allNodes are filled with valid nodes.
		bool allNodesPresent = true;
		foreach (SplineNode node in allNodes)
		{
			if (node == null)
			{
				allNodesPresent = false;
			}
		}

		if (allNodesPresent)
		{
			// There needs to be atleast 4 nodes to make a spline
			if (allNodes.Length < 4)
			{
				Debug.LogWarning("Need atleast 4 nodes to make a spline.");
			}
			else
			{
				// Used to fix a bug when creating the first point
				bool firstPoint = true;
				linearSplinePoints.Add(allNodes[0].transform.position);

				// Go through every node
				for (int nodeIndex = 0;
				        nodeIndex <= allNodes.Length - 2;
				        nodeIndex++)
				{
					// Move from the beginning of the node (0.0) to the next node (1.0)
					for (float t = 0.0f;
					        t <= 1.0f;
					        t += 0.01f)
					{
						SplineEquation(allNodes[nodeIndex], allNodes[nodeIndex + 1], t, firstPoint);
						firstPoint = false;
					}
				}

				if (closeSpline)
				{
					// Move from the beginning of the node (0.0) to the next node (1.0)
					for (float t = 0.0f;
					        t <= 1.0f;
					        t += 0.01f)
					{
						SplineEquation(allNodes[allNodes.Length - 1], allNodes[0], t, false);
					}
				}
			}
		}
		else
		{
			Debug.LogWarning("There is an empty node slot, fill it with a node.");
		}
	}

	public void SplineEquation(SplineNode node0, SplineNode node1, float t, bool firstPoint)
	{
		// starting tangent
		Vector3 m0 = node0.handlesPointing * 1.2f;
		// ending tangent
		Vector3 m1 = node1.handlesPointing * 1.2f;

		float splinePointX = SplineEqu(t, node0.transform.position.x, m0.x, m1.x);
		float splinePointY = SplineEqu(t, node0.transform.position.y, m0.y, m1.y);
		float splinePointZ = SplineEqu(t, node0.transform.position.z, m0.z, m1.z);

		if (node0.transform.position != Vector3.zero &&
		        node1.transform.position != Vector3.zero)
		{
			// Create a Vector3 from the calcuated points
			Vector3 newPoint = new Vector3(splinePointX, splinePointY, splinePointZ);

			if (!firstPoint)
			{
				// Creates the linear spline points
				distanceSinceLast += Vector3.Distance(newPoint, splinePoints[splinePoints.Count - 1]);

				if (distanceSinceLast > 0.5f)
				{
					distanceSinceLast = 0;
					linearSplinePoints.Add(newPoint);
				}
			}
			// Create the point
			splinePoints.Add(newPoint);
		}
	}

	// Forumla to calculate spline points
	public float SplineEqu(float t, float dimension, float inDimension, flaot outDimension)
	{
		return ((((2 * Mathf.Pow(t, 3)) - (3 * Mathf.Pow(t, 2)) + 1) * dimension) +
		        ((Mathf.Pow(t, 3) - (2 * Mathf.Pow(t, 2)) + t) * inDimension) +
		        (((-2 * Mathf.Pow(t, 3)) + (3 * Mathf.Pow(t, 2))) * dimension) +
		        ((Mathf.Pow(t, 3) - Mathf.Pow(t, 2)) * outDimension));
	}

	// Called when Unity wants to draw gizmos
	public void OnDrawGizmos()
	{
		// This draws the spline in the editor
		Gizmos.color = splineColor;

		for (int index = 0; index < splinePoints.Count - 2; index++)
		{
			Gizmos.DrawLine(splinePoints[index], splinePoints[index + 1]);
		}
	}
}