namespace HorrorEngine
{
    public interface ISavable<T>
    {
        T GetSavableData();

        void SetFromSavedData(T savedData);
    }
}