namespace Personal.Dashboard.Test.Support;

public static class DataFactory
{
    public static T[] Many<T>(Func<T> factory, int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => factory())
            .ToArray();
    }
}