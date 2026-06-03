using System;
using System.Web.Helpers;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Demo@1234 -> " + Crypto.HashPassword("Demo@1234"));
        Console.WriteLine("Password123! -> " + Crypto.HashPassword("Password123!"));
        Console.WriteLine("Pass1234 -> " + Crypto.HashPassword("Pass1234"));
        Console.WriteLine("Enkw@610230 -> " + Crypto.HashPassword("Enkw@610230"));
        Console.WriteLine("Star!@#$1 -> " + Crypto.HashPassword("Star!@#$1"));
    }
}
