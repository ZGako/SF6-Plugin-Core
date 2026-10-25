namespace SF6_Plugin_Core.Memory;

public static class MemoryHelpers
{
    /// <summary>
    ///     Attempts to retrieve a managed proxy of type T from a given memory address associated with a ManagedObject.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="address"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static T GetAddressAs<T>(ulong address) where T : class
    {
        var managedObj = ManagedObject.ToManagedObject(address)
                         ?? throw new ArgumentException(
                             $"Failed to convert pointer address {address} to a ManagedObject.");

        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
            if (!actualType.IsDerivedFrom(requestedType))
                throw new InvalidCastException(
                    $"Strict Cast Failed: Object at address {address} is of type '{actualType.FullName}', " +
                    $"which does not match or derive from the requested type '{requestedType.FullName}'.");

        // API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        return managedObj.As<T>() ??
               throw new InvalidCastException($"Proxy creation failed for object at address {address}.");
    }

    /// <summary>
    ///     Attempts to retrieve a managed proxy of type T from a given object, which is expected to be a ManagedObject.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="exceptionMessage">Message to display in case of an exception.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static T GetObjectAs<T>(this object obj, string? exceptionMessage = null) where T : class
    {
        var managedObj = obj as ManagedObject ??
                         throw new ArgumentException(exceptionMessage ?? "Object is not a ManagedObject or is null.");
        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
            if (!actualType.IsDerivedFrom(requestedType))
                throw new InvalidCastException(exceptionMessage ??
                                               $"Strict Cast Failed: Object at address {managedObj.GetAddress()} is of type '{actualType.FullName}', " +
                                               $"which does not match or derive from the requested type '{requestedType.FullName}'.");

        // API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        return managedObj.As<T>() ?? throw new InvalidCastException(exceptionMessage ??
                                                                    $"Proxy creation failed for object at address {managedObj.GetAddress()}.");
    }

    /// <summary>
    ///     Copies all non-static fields from a source ManagedObject to a target ManagedObject of the same type.
    /// </summary>
    /// <param name="target">The target ManagedObject to copy fields to.</param>
    /// <param name="source">The source ManagedObject to copy fields from.</param>
    /// <param name="typeDef">The TypeDefinition of the objects.</param>
    public static void CopyFieldsFrom(this ManagedObject target, ManagedObject source, TypeDefinition typeDef)
    {
        var sourceAddr = source.GetAddress();
        var targetAddr = target.GetAddress();

        foreach (var field in typeDef.GetFields())
        {
            if (field.IsStatic()) continue;
            try
            {
                if (field.Type.IsValueType())
                {
                    var size = field.Type.ValueTypeSize;
                    var buffer = new byte[size];
                    Marshal.Copy((IntPtr)(sourceAddr + field.OffsetFromBase), buffer, 0, (int)size);
                    Marshal.Copy(buffer, 0, (IntPtr)(targetAddr + field.OffsetFromBase), (int)size);
                }
                else
                {
                    var value = field.GetDataBoxed(sourceAddr, false);
                    field.SetDataBoxed(targetAddr, value, false);
                }
            }
            catch (Exception ex)
            {
                API.LogWarning($"Failed to copy field {field.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Sets a Guid value to a specified field of a ManagedObject.
    /// </summary>
    /// <param name="obj">The ManagedObject to set the Guid on.</param>
    /// <param name="fieldName">The name of the field to set.</param>
    /// <param name="guid">The Guid value to set.</param>
    public static void SetGuidField(this ManagedObject obj, string fieldName, Guid guid)
    {
        var typeDef = obj.GetTypeDefinition();
        var field = typeDef.GetField(fieldName);
        if (field == null)
        {
            API.LogWarning($"Field {fieldName} not found on {typeDef.FullName}");
            return;
        }

        var destAddress = (IntPtr)(obj.GetAddress() + field.OffsetFromBase);
        Marshal.Copy(guid.ToByteArray(), 0, destAddress, 16);
    }

    /// <summary>
    ///     Creates a new instance of System.Collections.Generic.List<T> in the RE Engine VM.
    /// </summary>
    public static ManagedObject? CreateGenericList<T>(bool globalize = true) where T : class
    {
        var closedListRuntimeType = ListTypeCache<T>.ClosedListRuntimeType;
        var createInstanceMethod = ListReflectionCache.CreateInstanceMethod;

        if (closedListRuntimeType == null || createInstanceMethod == null)
        {
            API.LogError(
                $"[MemoryHelpers] Failed to resolve cached RuntimeType or Activator for List<{typeof(T).FullName}>.");
            return null;
        }

        var newListMo = createInstanceMethod.InvokeBoxed(
            typeof(ManagedObject),
            null,
            new object[] { closedListRuntimeType }
        ) as ManagedObject;

        if (newListMo == null)
        {
            API.LogError($"[MemoryHelpers] Activator.CreateInstance failed for List<{typeof(T).FullName}>.");
            return null;
        }

        if (globalize)
            newListMo.Globalize();

        return newListMo;
    }

    // public static ManagedObject? CreateGenericList<T>(bool globalize = true) where T : class
    // {
    //     var tdb = API.GetTDB();
    //
    //     // system.type typedef
    //     var sysTypeDef = tdb.FindType("System.Type");
    //     // get elementRuntime type
    //     var elemRuntimeType = TypeDefCache<T>.RequestedType?.RuntimeType;
    //     // Get the RuntimeType of the open generic: typeof(System.Collections.Generic.List<>)
    //     var openListRuntimeType = tdb.FindType("System.Collections.Generic.List`1")?.RuntimeType;
    //     if (openListRuntimeType == null)
    //     {
    //         var getTypeMethod = sysTypeDef.GetMethod("GetType(System.String)");
    //         var typeNameStr = VM.CreateString("System.Collections.Generic.List`1");
    //         openListRuntimeType =
    //             getTypeMethod.InvokeBoxed(typeof(ManagedObject), null, [typeNameStr]) as ManagedObject;
    //     }
    //
    //     // create an array to be able to create with said type as its backing array
    //     var typeArrayMo = sysTypeDef.CreateManagedArray(1);
    //     typeArrayMo.As<_System.Array>().SetValue(elemRuntimeType, 0);
    //
    //     var closedListRuntimeType = (openListRuntimeType as IObject)
    //         .Call("MakeGenericType(System.Type[])", typeArrayMo) as ManagedObject;
    //
    //     // Create the actual List<app.Option.OptionUnitBase> instance from closedListRuntimeType
    //     var activatorType = tdb.FindType("System.Activator");
    //     var createInstanceMethod = activatorType.GetMethod("CreateInstance(System.Type)");
    //
    //     var newListMo = createInstanceMethod.InvokeBoxed(
    //         typeof(ManagedObject),
    //         null,
    //         [closedListRuntimeType]
    //     ) as ManagedObject;
    //
    //     return newListMo;
    // }

    /// <summary>
    ///     A nested static class that caches the TypeDefinition of a given type T for fast access.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private static class TypeDefCache<T> where T : class
    {
        public static readonly TypeDefinition? RequestedType;

        static TypeDefCache()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
                                                         System.Reflection.BindingFlags.Static;

            // Your exact reflection logic, but it only ever runs once!
            RequestedType = (typeof(T).GetField("REFType", flags)?.GetValue(null)
                             ?? typeof(T).GetProperty("REFType", flags)?.GetValue(null)) as TypeDefinition;
        }
    }

    /// <summary>
    ///     Caches global reflection metadata needed to construct generic List<T> instances.
    /// </summary>
    private static class ListReflectionCache
    {
        public static readonly TypeDefinition? SysTypeDef;
        public static readonly ManagedObject? OpenListRuntimeType;
        public static readonly Method? CreateInstanceMethod;

        static ListReflectionCache()
        {
            var tdb = API.GetTDB();
            SysTypeDef = tdb.FindType("System.Type");

            OpenListRuntimeType = tdb.FindType("System.Collections.Generic.List`1")?.RuntimeType;
            if (OpenListRuntimeType == null && SysTypeDef != null)
            {
                var getTypeMethod = SysTypeDef.GetMethod("GetType(System.String)");
                var typeNameStr = VM.CreateString("System.Collections.Generic.List`1");
                OpenListRuntimeType = getTypeMethod?.InvokeBoxed(
                    typeof(ManagedObject),
                    null,
                    new object[] { typeNameStr }
                ) as ManagedObject;
            }

            OpenListRuntimeType?.Globalize();

            var activatorType = tdb.FindType("System.Activator");
            CreateInstanceMethod = activatorType?.GetMethod("CreateInstance(System.Type)");
        }
    }

    /// <summary>
    ///     Caches the closed System.RuntimeType for List<T> using the exact MakeGenericType method.
    /// </summary>
    private static class ListTypeCache<T> where T : class
    {
        public static readonly ManagedObject? ClosedListRuntimeType;

        static ListTypeCache()
        {
            // 1. Get the RuntimeType of the element T
            var elemRuntimeType = TypeDefCache<T>.RequestedType?.RuntimeType;
            var openListRuntimeType = ListReflectionCache.OpenListRuntimeType;
            var sysTypeDef = ListReflectionCache.SysTypeDef;

            if (elemRuntimeType == null || openListRuntimeType == null || sysTypeDef == null)
                return;

            // 2. Create System.Type[] containing elemRuntimeType
            var typeArrayMo = sysTypeDef.CreateManagedArray(1);
            typeArrayMo.As<_System.Array>().SetValue(elemRuntimeType, 0);

            // 3. Build the closed RuntimeType via MakeGenericType
            ClosedListRuntimeType = (openListRuntimeType as IObject)
                ?.Call("MakeGenericType(System.Type[])", typeArrayMo) as ManagedObject;

            ClosedListRuntimeType?.Globalize();
        }
    }
}