using SF6_Plugin_Core.UI.InjectionShared;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class UICustomElementRequestNode(Guid functionId, IUICustomTrainingElementInitialization customInitializer)
    : UICustomRequestNode<app.training.TrainingMenuData, IUICustomTrainingElementInitialization>(functionId,
        customInitializer)
{
    internal override app.training.TrainingMenuData Build()
    {
        // call the custom initializer to initialize the custom element
        // this way the function types will be numbered similarly to the game (doesn't actually matter)
        var customElementData = CustomInitializer.InitializeCustomElement(FunctionId);

        ChildDataArrayMo?.ReleaseIfGlobalized();
        // create a new array (a bit of a waste to do it every time, but it shouldn't happen often enough to really matter anyways)
        ChildDataArrayMo = app.training.TrainingMenuData.REFType.CreateManagedArray((uint)ChildNodes.Count);
        ChildDataArrayMo.Globalize();

        var childDataArray = ChildDataArrayMo.As<app.training.TrainingMenuData_Array1D>();
        if (ChildNodes.Count > 0)
            for (var i = 0; i < ChildNodes.Count; i++)
            {
                var childData = ChildNodes[i].Build();
                childDataArray[i] = childData;
            }

        // assign the childData array to the custom element
        customElementData.ChildData = childDataArray;


        // add the hooks to the custom element
        foreach (var hook in HookInstructions) hook.AddHook(FunctionId);

        return customElementData;
    }
}