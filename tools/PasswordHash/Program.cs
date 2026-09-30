using Microsoft.AspNetCore.Identity;

var password = args.Length == 0 ? "Admin@123" : args[0];
Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), password));
