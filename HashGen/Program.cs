using System;
using CoreKit.Identity.Security;

var password = args.Length > 0 ? args[0] : "TestPass123!";
var hasher = new CoreKit.Identity.Security.PasswordHasher();
var hash = hasher.Hash(password);
Console.WriteLine(hash);