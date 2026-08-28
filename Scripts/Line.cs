using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Line : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [FormerlySerializedAs("_segmentcount")]
    [Header("Trajectory line smmothness")]
    [SerializeField] private int segmentcount = 50;

    private Vector3[] _segments;            //
    private LineRenderer _lineRenderer;
     
    void Start()
    {
        _segments = new Vector3[segmentcount]; //trajectory라인은 결국 선을 이어서 그림. 50개의 점들을 segments에 할당. 
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = segmentcount; //linerenderer가 몇개의 점으로 선을 그리리지 먼저 설정. 안하니까 선안그려짐 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
