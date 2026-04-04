namespace HorrorEngine
{
    public abstract class SettingsElementSliderContent : SettingsElementContent
    {
        public float MinSliderValue = 0f;
        public float MaxSliderValue = 1f;
        public float DefaultValue = 0f;
        public bool WholeNumbers = false;
        public string TextFormat = "{0}";

        public override string GetDefaultValue()
        {
            return DefaultValue.ToString();
        }
    }
}
