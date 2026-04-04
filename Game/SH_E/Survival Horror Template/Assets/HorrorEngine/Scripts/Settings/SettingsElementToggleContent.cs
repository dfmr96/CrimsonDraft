namespace HorrorEngine
{
    public abstract class SettingsElementToggleContent : SettingsElementContent
    {
        public bool DefaultValue;

        public override string GetDefaultValue()
        {
            return DefaultValue.ToString();
        }
    }
}
