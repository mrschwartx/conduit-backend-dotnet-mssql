namespace Conduit.SharedKernel.Domain;

public static class Messages
{
    public const string Blank = "can't be blank";

    public const string Invalid = "is invalid";

    public const string Taken = "has already been taken";

    public const string Empty = "can't be empty";

    public static string TooShort(int minimum) => $"is too short (minimum is {minimum} characters)";

    public static string TooLong(int maximum) => $"is too long (maximum is {maximum} characters)";
}
