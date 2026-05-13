using System.Collections.Generic;

namespace FanControl.Plugins
{
    public interface IPlugin
    {
        string Name { get; }

        void Initialize();

        void Load(IPluginSensorsContainer container);

        void Close();
    }

    public interface IPlugin2 : IPlugin
    {
        void Update();
    }

    public interface IPluginLogger
    {
        void Log(string message);
    }

    public interface IPluginSensor
    {
        string Id { get; }

        string Name { get; }

        string Origin { get; }

        float? Value { get; }

        void Update();
    }

    public interface IPluginControlSensor : IPluginSensor
    {
        void Set(float value);

        void Reset();
    }

    public interface IPluginControlSensor2 : IPluginControlSensor
    {
        string PairedFanSensorId { get; }
    }

    public interface IPluginSensorsContainer
    {
        IList<IPluginSensor> FanSensors { get; }

        IList<IPluginControlSensor> ControlSensors { get; }
    }
}
