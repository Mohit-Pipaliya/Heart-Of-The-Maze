using System;
using UnityEngine;

class Program
{
    static void Main()
    {
        Console.WriteLine(Animator.StringToHash("Unequip"));
        Console.WriteLine(Animator.StringToHash("UnequipGun"));
        Console.WriteLine(Animator.StringToHash("unequip"));
        Console.WriteLine(Animator.StringToHash("Unequipped"));
    }
}
