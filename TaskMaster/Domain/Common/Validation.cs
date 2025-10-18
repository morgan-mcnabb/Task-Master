namespace Domain.Common;

public static class Validation
{
    public const int UserIdMaxLength = 256;

    public static class Task
    {
        public const int TitleMaxLength = 120;
        public const int DescriptionMaxLength = 2000;
    }

    public static class Tag
    {
        public const int NameMaxLength = 64;
        public const int NormalizedNameMaxLength = 64;
        public const int KeyMaxLength = 128; 
    }
}