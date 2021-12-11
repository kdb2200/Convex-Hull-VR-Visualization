using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Play : MonoBehaviour
{
    public GameObject DnC_Object;

    private DivideAndConquer DnC;

    // Start is called before the first frame update
    void Start()
    {
        DnC = DnC_Object.GetComponent<DivideAndConquer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    void Interact()
    {
        DnC.SetIsPlaying(true);
    }
}
