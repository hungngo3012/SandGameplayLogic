using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shields : MonoBehaviour
{
    [SerializeField] SpriteRenderer shieldRenderer;

    internal void SetColor(Color color)
    {
        shieldRenderer.color = color;
    }
}
