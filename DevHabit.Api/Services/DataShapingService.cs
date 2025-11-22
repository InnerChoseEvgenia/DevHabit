using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;

namespace DevHabit.Api.Services
{

    public sealed class DataShapingService
    {
        //ConcurrentDictionary - используется в данном случае для кеширования запроса,
        //т.к. рефлексия из методов этого сервиса - влияет на перфоманс
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();
        // ExpandoObject из using System.Dynamic, позволяет добавлять тип во время рантайма,
        // т.е. заранее не нужно указывать тип, что удобно с shaping, т.к. для шейпинга м.
        // указываться разные поля,имеющие разный тип
        // аргумент string? fields налабел, т.к. пользователь не всегда будет пользовать дата шейпингом

        //метод для возврата из эндпоита GetHabit
        public ExpandoObject ShapeData<T>(T entity, string? fields)
        {
            HashSet<string> fieldsSet = fields?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
                typeof(T),
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

            if (fieldsSet.Any())
            {
                propertyInfos = propertyInfos
                    .Where(p => fieldsSet.Contains(p.Name))
                    .ToArray();
            }

            IDictionary<string, object?> shapedObject = new ExpandoObject();

            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                shapedObject[propertyInfo.Name] = propertyInfo.GetValue(entity);
            }

            return (ExpandoObject)shapedObject;
        }

        //метод для возврата List<ExpandoObject> из эндпоита GetHabits
        public List<ExpandoObject> ShapeCollectionData<T>(IEnumerable<T> entities, string? fields)
        {
            HashSet<string> fieldsSet = fields?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            // GetOrAdd добавляет результат запроса в кеш PropertiesCache
            PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
                typeof(T),
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

            //по названию запроса для шейпинга и конвертируем в арэй
            //шейпинг будет осуществляться только при наличии такого запроса 
            // и это проверяет if (fieldsSet.Any())
            if (fieldsSet.Any())
            {
                propertyInfos = propertyInfos
                    .Where(p => fieldsSet.Contains(p.Name))
                    .ToArray();
            }

            //присваиваем арэй List<ExpandoObject>
            List<ExpandoObject> shapedObjects = [];
            foreach (T entity in entities)
            {
                IDictionary<string, object?> shapedObject = new ExpandoObject();

                foreach (PropertyInfo propertyInfo in propertyInfos)
                {
                    shapedObject[propertyInfo.Name] = propertyInfo.GetValue(entity); //берет в качестве ключа propertyInfo.Name
                }

                shapedObjects.Add((ExpandoObject)shapedObject);
            }

            return shapedObjects;
        }
        //метод проверяет есть ли такое поле в нашем приложении,
        //чтобы пользователь не ввел несуществующего fields
        public bool Validate<T>(string? fields)
        {
            if (string.IsNullOrWhiteSpace(fields))
            {
                return true;
            }

            var fieldsSet = fields
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
                typeof(T),
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

            return fieldsSet.All(f => propertyInfos.Any(p => p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
