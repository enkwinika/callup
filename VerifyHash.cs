using System;
using System.Web.Helpers;

public class Program {
    public static void Main() {
        string targetHash = "ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==";
        string[] candidates = { 
            "Test@123", "Password123!", "Demo@1234", "Pass1234", "Enkw@610230", "Star!@#$1",
            "Admin@123", "abc@123", "Abc@123", "password", "Password123", 
            "123456", "admin", "Admin123", "Admin123!", "welcome", "welcome123", "abc",
            "Abc123!", "abc12345", "Test1234", "Test1234!", "@Frih0st@610230", "nWEREy?Z4%EQfH"
        };
        
        foreach (var c in candidates) {
            try {
                if (Crypto.VerifyHashedPassword(targetHash, c)) {
                    Console.WriteLine("MATCH FOUND: " + c);
                    return;
                }
            } catch {}
        }
        Console.WriteLine("No match found in current candidates.");
    }
}
