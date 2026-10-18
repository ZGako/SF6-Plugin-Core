using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public abstract class CustomOptionBase(CustomOptionBase.CustomOptionInitializer initializer)
    : IUICustomOptionsElementInitialization
{
    protected readonly CustomOptionInitializer _initializer = initializer;

    public app.Option.OptionUnitBase InitializeCustomElement(Guid functionNameId)
    {
        return _initializer.InitializeCustomElement(functionNameId);
    }

    public void Clear()
    {
        _initializer.Clear();
    }

    public app.Option.OptionUnitBase GetElementData()
    {
        return _initializer.GetElementData();
    }

    protected class CustomOptionInitializer : IUICustomOptionsElementInitialization
    {
        // class specific fields
        private ManagedObject? _customOptionDataMo;

        public void Clear()
        {
        }

        public app.Option.OptionUnitBase InitializeCustomElement(Guid functionNameId)
        {
            throw new NotImplementedException();
        }

        public app.Option.OptionUnitBase GetElementData()
        {
            throw new NotImplementedException();
        }
    }
}