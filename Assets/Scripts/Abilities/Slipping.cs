using UnityEngine;

public class Slipping : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    // used to ensure that the player is slipping on the slippery surface and not just walking on it
    void onTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            other.gameObject.GetComponent<Rigidbody>().AddForce(transform.forward * 10f, ForceMode.Acceleration);
        }
    }
}
