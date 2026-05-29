using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Curved : GameUnit
{
	public const float HZ = 1 / 50f;

	public Transform model;

	public AnimationCurve curve;

	public float bodySize;

    private void OnDisable()
    {
		StopAllCoroutines();
    }

    public void SetTarget(Vector3 startPos, Transform target, UnityAction doneAction)
	{
		model.localScale = Vector3.one;

		StartCoroutine(IEMoveCurve(startPos, target, tf, doneAction));
	}

	public virtual void Progress(Vector3 target, float ratio)
    {
		model.localScale = Vector3.one * curve.Evaluate(ratio);
	}

	private IEnumerator IEMoveCurve(Vector3 startPoint, Transform target, Transform item, UnityAction callBack)
	{
		Vector3 start = startPoint;
		Vector3 finish = target.position;

		//chieu cao test
		Vector3 height = new Vector3((start.x + finish.x) / 2, 7, (start.z + finish.z) / 2);

		float ratio = 0;

		while (ratio <= 1)
		{
			var tangent1 = Vector3.Lerp(start, height, ratio);
			var tangent2 = Vector3.Lerp(height, target.position, ratio);
			var curve = Vector3.Lerp(tangent1, tangent2, ratio);

			ratio += HZ;
			Progress(curve, ratio);
			item.position = curve;

			yield return null;
		}

		callBack?.Invoke();
	}

	private Vector3 GetOnCircle(float y)
    {
		Vector2 onCircle = Random.insideUnitCircle;
		return new Vector3(onCircle.x, y, onCircle.y);
    }

    public override void OnInit()
    {
        throw new System.NotImplementedException();
    }

    public override void OnDespawn()
    {
        throw new System.NotImplementedException();
    }


    //private void OnDrawGizmos()
    //{
    //    Gizmos.DrawWireSphere(tf.position, bodySize);
    //}
}
