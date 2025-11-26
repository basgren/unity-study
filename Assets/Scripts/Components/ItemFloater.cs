using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemFloater : MonoBehaviour {
    [SerializeField]
    private float period = 2.0f;
    
    [SerializeField]
    private float amplitude = 0.2f;

    [Range(-1.0f, 1.0f)]
    [SerializeField]
    private float initialPhase;

    [SerializeField]
    private bool randomInitialPhase;

    private float time;
    private Vector3 initialPosition;
    
    void Start() {
        initialPosition = transform.position;
        
        if (randomInitialPhase) {
            initialPhase = Random.Range(-1.0f, 1.0f);
        }
    }

    void Update() {
        var y = Mathf.Cos(time * Mathf.PI * 2.0f / period + initialPhase) * amplitude;
        transform.position = initialPosition + new Vector3(0, y, 0);
        time += Time.deltaTime;
    }
}
