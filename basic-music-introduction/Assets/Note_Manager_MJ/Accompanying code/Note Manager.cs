using System.Collections.Generic;
using Data_holders.instruments;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    private (Instruments, float) CH(float duration)
    {
        return (Instruments.Closed_HiHat, duration);
    }
    
    private (Instruments, float) OH(float duration)
    {
        return (Instruments.Open_HiHat, duration);
    }
    
    private (Instruments, float) Kick(float duration)
    {
        return (Instruments.Kick, duration);
    }

    private (Instruments, float) Snare(float duration)
    {
        return (Instruments.Snare, duration);
    }

    private List<(Instruments, float)> notes = new();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        //here goes the logic for filling in the duration in the list baseed on the color of the scan input
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
