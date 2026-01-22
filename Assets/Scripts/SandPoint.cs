using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandPoint : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;

    internal void SetColor(Color color)
    {
        spriteRenderer.color = color;
    }
}
