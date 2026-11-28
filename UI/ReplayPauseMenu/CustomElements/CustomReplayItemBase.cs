using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.CustomElements;

/// <summary>
///     Abstract base initializer for custom <see cref="app.ReplayMenuItem" /> entries injected into SF6's Replay
///     Pause Menu, managing globalized native objects and <see cref="CustomMessage" /> registrations.
/// </summary>
public abstract class CustomReplayItemBase : IUICustomReplayElementInitialization
{
    private readonly List<ManagedObject> _childDataElementMos = [];
    private ManagedObject? _childDataArrayMo;
    protected ManagedObject? _customItemMo;

    /// <summary>Widget presentation kind (<see cref="app.ReplayMenuItem.EItemType.Button" /> or <see cref="app.ReplayMenuItem.EItemType.Spin" />).</summary>
    public app.ReplayMenuItem.EItemType ItemType { get; init; } = app.ReplayMenuItem.EItemType.Button;

    /// <summary>Button visual variant (<see cref="app.ReplayMenuItem.EButtonType" />).</summary>
    public app.ReplayMenuItem.EButtonType ButtonType { get; init; } = app.ReplayMenuItem.EButtonType.Type0;

    /// <summary>Target Replay Pause Menu tab (<see cref="app.ReplayMenuItem.ETabType" />).</summary>
    public app.ReplayMenuItem.ETabType TabType { get; init; } = app.ReplayMenuItem.ETabType.DispSetting;

    /// <summary>Primary row label written into native <c>ItemMsg</c>.</summary>
    public CustomMessage? ItemMsg { get; init; }

    /// <summary>Spinner option labels converted into <c>app.ReplayChildData[]</c> (used when <see cref="ItemType" /> is <c>Spin</c>).</summary>
    public List<CustomMessage>? ChildMessages { get; init; }

    /// <summary>
    ///     Optional callback evaluated when <c>UIReplayPauseMenu.Param.InitMenuList</c> opens the pause menu,
    ///     seeding <c>DefaultFocusIndex</c> so spinners show their live backing value instead of index <c>0</c>.
    /// </summary>
    public Func<int>? GetInitialSpinIndex { get; init; }

    /// <inheritdoc />
    public virtual app.ReplayMenuItem InitializeCustomElement(Guid functionNameId)
    {
        EnsureMessageRegistration();

        var funcTypeInt = ReplayFuncTypeRegistry.RegisterNewFunctionType(functionNameId);

        if (_customItemMo != null)
        {
            var existing = _customItemMo.As<app.ReplayMenuItem>();
            existing.FuncType = (app.esports.UIReplayPauseMenu.EReplayFuncType)funcTypeInt;
            return existing;
        }

        var itemMo = app.ReplayMenuItem.REFType.CreateInstance(0);
        itemMo.Globalize();
        _customItemMo = itemMo;

        var item = itemMo.As<app.ReplayMenuItem>();
        item.FuncType = (app.esports.UIReplayPauseMenu.EReplayFuncType)funcTypeInt;
        item.ItemType = ItemType;
        item.ButtonType = ButtonType;
        item.TabType = TabType;

        if (ItemMsg != null) itemMo.SetGuidField("ItemMsg", ItemMsg.Id);

        var childCount = ChildMessages?.Count ?? 0;
        var childArrayMo = app.ReplayChildData.REFType.CreateManagedArray((uint)childCount);
        childArrayMo.Globalize();
        _childDataArrayMo = childArrayMo;

        var childArray = childArrayMo.As<app.ReplayChildData_Array1D>();
        if (ChildMessages != null)
            for (var i = 0; i < ChildMessages.Count; i++)
            {
                var childMo = app.ReplayChildData.REFType.CreateInstance(0);
                childMo.Globalize();
                _childDataElementMos.Add(childMo);

                childMo.SetGuidField("Msg", ChildMessages[i].Id);
                childArray[i] = childMo.As<app.ReplayChildData>();
            }

        item.ChildData = childArray;
        return item;
    }

    /// <inheritdoc />
    public virtual app.ReplayMenuItem GetElementData()
    {
        return _customItemMo?.As<app.ReplayMenuItem>() ??
               throw new InvalidOperationException("Custom replay element data has not been initialized.");
    }

    /// <inheritdoc />
    public virtual void Clear()
    {
        ItemMsg?.Dispose();
        if (ChildMessages != null)
            foreach (var msg in ChildMessages)
                msg.Dispose();

        _childDataArrayMo?.ReleaseIfGlobalized();
        _childDataArrayMo = null;

        foreach (var childMo in _childDataElementMos)
            childMo.ReleaseIfGlobalized();
        _childDataElementMos.Clear();

        _customItemMo?.ReleaseIfGlobalized();
        _customItemMo = null;
    }

    protected void EnsureMessageRegistration()
    {
        ItemMsg?.EnsureRegistration();
        if (ChildMessages != null)
            foreach (var msg in ChildMessages)
                msg.EnsureRegistration();
    }
}

