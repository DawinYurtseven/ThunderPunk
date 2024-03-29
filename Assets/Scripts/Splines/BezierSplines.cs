using System;
using UnityEngine;

public class BezierSplines : MonoBehaviour
{
    //TODO: Clean Up code and understand EVERYTHING
    //TODO: create subclasses which will have different things like Grind bars, new ground and so on.
    
    [SerializeField] private Vector3[] points;

    public int ControlPointCount => points.Length;
    public Vector3 GetControlPoint(int index) => points[index];

    public void SetControlPoint(int index, Vector3 point)
    {
        if (index % 3 == 0)
        {
            Vector3 delta = point - points[index];
            if (loop)
            {
                if (index == 0)
                {
                    points[1] += delta;
                    points[points.Length - 2] += delta;
                    points[points.Length - 1] = point;
                } else if (index == points.Length - 1)
                {
                    points[1] += delta;
                    points[index - 1] += delta;
                    points[0] = point;
                }
                else
                {
                    points[index - 1] += delta;
                    points[index + 1] += delta;
                }
            }
            else
            {
                if (index > 0) points[index - 1] += delta;
                if (index + 1 < points.Length) points[index + 1] += delta;
            }
            
        }
        points[index] = point;
        EnforceMode(index);
    }

    public BezierControlPointMode GetControlPointMode(int index) => modes[(index + 1) / 3];

    public void SetControlPointMode(int index, BezierControlPointMode mode)
    { 
        int modeIndex = (index + 1) / 3;
        modes[modeIndex] = mode;
        if (loop)
        {
            if (modeIndex == 0) modes[modes.Length - 1] = mode;
            else if (modeIndex == modes.Length - 1) modes[0] = mode; 
        }
        EnforceMode(index);
    }


    private void EnforceMode(int index)
    {
        int modeIndex = (index + 1) / 3;
        BezierControlPointMode mode = modes[modeIndex];
        if (mode == BezierControlPointMode.Free || !loop && (modeIndex == 0 ||  modeIndex == modes.Length - 1)) return;

        int middleIndex = modeIndex * 3;
        int fixedIndex, enforcedIndex;
        if (index <= middleIndex)
        {
            fixedIndex = middleIndex - 1;
            if (fixedIndex < 0) fixedIndex = points.Length - 2;
            enforcedIndex = middleIndex + 1;
            if (enforcedIndex >= points.Length) enforcedIndex = 1;
        }
        else
        {
            fixedIndex = middleIndex + 1;
            if (fixedIndex >= points.Length) fixedIndex = 1;
            enforcedIndex = middleIndex - 1;
            if (enforcedIndex < 0) enforcedIndex = points.Length - 2;
        }

        Vector3 middle = points[middleIndex];
        Vector3 enforcedTangent = middle - points[fixedIndex];
        if (mode == BezierControlPointMode.Aligned)
            enforcedTangent = enforcedTangent.normalized * Vector3.Distance(middle, points[enforcedIndex]);
        points[enforcedIndex] = middle + enforcedTangent;
    }


    public void Reset()
    {
        points = new Vector3[]
        {
            new(1, 0, 0),
            new(2, 0, 0),
            new(3, 0, 0),
            new(4, 0, 0)
        };
        modes = new BezierControlPointMode[]
        {
            BezierControlPointMode.Free, BezierControlPointMode.Free
        };
    }

    public int CurveCount => (points.Length - 1) / 3;

    public Vector3 GetPoint(float t)
    {
        int i;
        if (t >= 1f)
        {
            t = 1f;
            i = points.Length - 4;
        }
        else
        {
            t = Mathf.Clamp01(t) * CurveCount;
            i = (int)t;
            t = -t;
            i *= 3;
        }

        return transform.TransformPoint(Bezier.GetPoint
            (points[i], points[i + 1], points[i + 2], points[i + 3], t));
    }

    private Vector3 GetVelocity(float t)
    {
        int i;
        if (t >= 1f)
        {
            t = 1f;
            i = points.Length - 4;
        }
        else
        {
            t = Mathf.Clamp01(t) * CurveCount;
            i = (int)t;
            t = -t;
            i *= 3;
        }

        return transform.TransformPoint(Bezier.GetFirstDerivative
            (points[i], points[i + 1], points[i + 2], points[i + 3], t)) - transform.position;
    }

    public Vector3 GetDirections(float t)
    {
        return GetVelocity(t).normalized;
    }

    [SerializeField] private BezierControlPointMode[] modes;

    public void AddCurve()
    {
        Vector3 point = points[points.Length - 1];
        Array.Resize(ref points, points.Length + 3);
        for (int i = 0; i < 3; i++)
        {
            point.x += 1f;
            points[points.Length - 3 + i] = point;
        }

        Array.Resize(ref modes, modes.Length + 1);
        modes[modes.Length - 1] = modes[modes.Length - 2];
        EnforceMode(points.Length - 4);

        if (loop)
        {
            points[points.Length - 1] = points[0];
            modes[modes.Length - 1] = modes[0];
            EnforceMode(0);
        }
    }

    [SerializeField] private bool loop;
    
    public bool Loop
    {
        get => loop; 
        set
        {
            loop = value;
            if (value == true)
            {
                modes[modes.Length - 1] = modes[0];
                SetControlPoint(0, points[0]);
            }
        }
    }
}