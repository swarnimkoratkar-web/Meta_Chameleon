using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PaintStroke
{
    public Vector2 uv;      
    public float radius;    
    public Color32 color;   
}

[CreateAssetMenu(fileName = "NewPaintData", menuName = "Camouflage/Paint Stroke Data")]
public class PaintStrokeData : ScriptableObject
{
    [Tooltip("List of recorded brush strokes for this hiding spot.")]
    public List<PaintStroke> strokes = new List<PaintStroke>();

    public void ClearStrokes()
    {
        strokes.Clear();
    }

    public void AddStroke(Vector2 uv, float radius, Color32 color)
    {
        strokes.Add(new PaintStroke
        {
            uv = uv,
            radius = radius,
            color = color
        });
    }
}