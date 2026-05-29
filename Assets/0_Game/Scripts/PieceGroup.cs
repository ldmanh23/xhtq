using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class PieceGroup : GameUnit
{
    public readonly List<Piece> pieces = new List<Piece>();

    public void Setup(List<Piece> groupPieces)
    {
        pieces.Clear();
        pieces.AddRange(groupPieces);
    }

    public bool Contains(Piece piece)
    {
        return pieces.Contains(piece);
    }

    public override void OnInit()
    {
    }

    public override void OnDespawn()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        pieces.Clear();
    }
}
