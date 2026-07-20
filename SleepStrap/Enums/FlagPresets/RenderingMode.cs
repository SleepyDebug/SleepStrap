namespace SleepStrap.Enums.FlagPresets
{
    public enum RenderingMode
    {
        [EnumName(FromTranslation = "Common.Automatic")]
        Default,
        Vulkan,
        OpenGL,
        D3D11,
    }
}
