using System;
using CoreKit.Identity.Security;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: HashGen <password>");
    Environment.Exit(1);
}

var password = args[0];
var hasher = new CoreKit.Identity.Security.PasswordHasher();
Console.WriteLine(hasher.Hash(password));