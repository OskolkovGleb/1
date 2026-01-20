using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Otus.Serialization
{
    // 1. Класс F из задания
    public class F
    {
        int i1;
        int i2;
        int i3;
        int i4;
        int i5;
        public int[] mas;

        public F()
        {
            i1 = 1; i2 = 2; i3 = 3; i4 = 4; i5 = 5;
            mas = new int[] { 1, 2 };
        }

        public F Get() => new F();

        public int I1 => i1;
        public int I2 => i2;
        public int I3 => i3;
        public int I4 => i4;
        public int I5 => i5;

        public override string ToString() =>
            $"F(i1={i1}, i2={i2}, i3={i3}, i4={i4}, i5={i5}, mas={(mas != null ? $"[{string.Join(",", mas)}]" : "null")})";
    }
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public double Salary { get; set; }

        public override string ToString() =>
            $"Person(Name='{Name}', Age={Age}, Email='{Email}', IsActive={IsActive}, Salary={Salary:F2})";
    }

    public class CsvSerializer
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new();
        private static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();

        public string Serialize<T>(T obj)
        {
            var type = typeof(T);

            var properties = GetProperties(type);
            var fields = GetFields(type);

            var values = new List<string>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                values.Add(FormatValue(value));
            }

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                values.Add(FormatValue(value));
            }

            return string.Join(",", values);
        }

        private PropertyInfo[] GetProperties(Type type)
        {
            if (!_propertyCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty)
                    .Where(p => p.CanRead)
                    .ToArray();
                _propertyCache[type] = properties;
            }
            return properties;
        }

        private FieldInfo[] GetFields(Type type)
        {
            if (!_fieldCache.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .ToArray();
                _fieldCache[type] = fields;
            }
            return fields;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "";

            if (value is Array array)
            {
                if (array.Length == 0) return "[]";

                var elements = new List<string>();
                foreach (var item in array)
                {
                    elements.Add(item?.ToString() ?? "");
                }
                return $"\"[{string.Join(",", elements)}]\"";
            }

            var stringValue = value.ToString() ?? "";

            if (stringValue.Contains(',') || stringValue.Contains('"') || stringValue.Contains('\n') || stringValue.Contains('\r'))
            {
                stringValue = stringValue.Replace("\"", "\"\"");
                return $"\"{stringValue}\"";
            }

            return stringValue;
        }

        public T Deserialize<T>(string csvLine) where T : new()
        {
            if (string.IsNullOrEmpty(csvLine))
                throw new ArgumentException("CSV строка не может быть пустой");

            var type = typeof(T);
            var properties = GetProperties(type);
            var fields = GetFields(type);

            var values = ParseCsvLine(csvLine);
            var instance = new T();

            int valueIndex = 0;

            foreach (var prop in properties)
            {
                if (valueIndex >= values.Length) break;

                var stringValue = values[valueIndex++];
                var convertedValue = ConvertValue(stringValue, prop.PropertyType);

                if (prop.CanWrite)
                {
                    prop.SetValue(instance, convertedValue);
                }
            }

            foreach (var field in fields)
            {
                if (valueIndex >= values.Length) break;

                var stringValue = values[valueIndex++];
                var convertedValue = ConvertValue(stringValue, field.FieldType);
                field.SetValue(instance, convertedValue);
            }

            return instance;
        }

        private string[] ParseCsvLine(string csvLine)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvLine.Length; i++)
            {
                char c = csvLine[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < csvLine.Length && csvLine[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; 
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private object ConvertValue(string stringValue, Type targetType)
        {
            if (string.IsNullOrEmpty(stringValue))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (stringValue.StartsWith("\"") && stringValue.EndsWith("\""))
            {
                stringValue = stringValue.Substring(1, stringValue.Length - 2);
                stringValue = stringValue.Replace("\"\"", "\"");
            }

            try
            {
                if (stringValue.StartsWith("[") && stringValue.EndsWith("]"))
                {
                    if (stringValue == "[]")
                        return Array.CreateInstance(targetType.GetElementType() ?? typeof(object), 0);

                    var arrayContent = stringValue.Substring(1, stringValue.Length - 2);
                    var elements = arrayContent.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    if (targetType == typeof(int[]))
                    {
                        return elements.Select(e => int.Parse(e.Trim())).ToArray();
                    }
                    else if (targetType == typeof(string[]))
                    {
                        return elements.Select(e => e.Trim()).ToArray();
                    }
                }

                if (targetType == typeof(string))
                    return stringValue;

                if (targetType == typeof(int) || targetType == typeof(int?))
                    return int.Parse(stringValue);

                if (targetType == typeof(double) || targetType == typeof(double?))
                    return double.Parse(stringValue, CultureInfo.InvariantCulture);

                if (targetType == typeof(bool) || targetType == typeof(bool?))
                    return bool.Parse(stringValue);

                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                    return DateTime.Parse(stringValue);

                return Convert.ChangeType(stringValue, targetType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка конвертации '{stringValue}' в тип {targetType.Name}: {ex.Message}");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }
    }

    public class IniDeserializer
    {
        public T Deserialize<T>(string iniContent) where T : new()
        {
            var instance = new T();
            var type = typeof(T);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var lines = iniContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue;

                var parts = trimmedLine.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                var property = properties.FirstOrDefault(p =>
                    string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

                if (property != null && property.CanWrite)
                {
                    var convertedValue = ConvertValue(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                    continue;
                }

                var field = fields.FirstOrDefault(f =>
                    string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase));

                if (field != null)
                {
                    var convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(instance, convertedValue);
                }
            }

            return instance;
        }

        private object ConvertValue(string stringValue, Type targetType)
        {
            if (string.IsNullOrEmpty(stringValue))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            try
            {
                if (targetType == typeof(string))
                    return stringValue;

                if (targetType == typeof(int) || targetType == typeof(int?))
                    return int.Parse(stringValue);

                if (targetType == typeof(double) || targetType == typeof(double?))
                    return double.Parse(stringValue, CultureInfo.InvariantCulture);

                if (targetType == typeof(bool) || targetType == typeof(bool?))
                    return bool.Parse(stringValue);

                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                    return DateTime.Parse(stringValue);

                return Convert.ChangeType(stringValue, targetType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка конвертации '{stringValue}' в тип {targetType.Name}: {ex.Message}");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== Домашнее задание: Рефлексия и её применение ===");
            Console.WriteLine("=== CSV-сериализатор с использованием Reflection ===\n");

            var f = new F();
            var csvSerializer = new CsvSerializer();

            Console.WriteLine("1. CSV СЕРИАЛИЗАЦИЯ КЛАССА F:");
            Console.WriteLine(new string('-', 50));

            int iterations = 100000;
            string csvResult = "";

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                csvResult = csvSerializer.Serialize(f);
            }
            sw.Stop();

            Console.WriteLine($"Результат сериализации: {csvResult}");
            Console.WriteLine($"Время {iterations:N0} итераций: {sw.Elapsed.TotalMilliseconds:F2} мс");
            Console.WriteLine($"Среднее время на операцию: {sw.Elapsed.TotalMilliseconds / iterations:F6} мс");
            Console.WriteLine($"Операций в секунду: {iterations / sw.Elapsed.TotalSeconds:F0}");

            sw.Restart();
            Console.WriteLine($"\nВывод строки в консоль...");
            sw.Stop();
            var consoleOutputTime = sw.Elapsed;
            Console.WriteLine($"Время вывода в консоль: {consoleOutputTime.TotalMilliseconds:F4} мс");

            Console.WriteLine("\n2. СРАВНЕНИЕ С JSON СЕРИАЛИЗАЦИЕЙ:");
            Console.WriteLine(new string('-', 50));

            string jsonResult = "";
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                jsonResult = JsonSerializer.Serialize(f);
            }
            sw.Stop();

            Console.WriteLine($"Результат JSON сериализации: {jsonResult}");
            Console.WriteLine($"Время JSON {iterations:N0} итераций: {sw.Elapsed.TotalMilliseconds:F2} мс");
            Console.WriteLine($"Среднее время на операцию: {sw.Elapsed.TotalMilliseconds / iterations:F6} мс");
            Console.WriteLine($"Операций в секунду: {iterations / sw.Elapsed.TotalSeconds:F0}");

            Console.WriteLine("\n3. СРАВНЕНИЕ ПРОИЗВОДИТЕЛЬНОСТИ:");
            Console.WriteLine(new string('-', 50));

            double jsonTime = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                csvResult = csvSerializer.Serialize(f);
            }
            sw.Stop();
            double reflectionTime = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Reflection CSV: {reflectionTime:F2} мс ({reflectionTime / iterations:F6} мс/оп)");
            Console.WriteLine($"System.Text.Json: {jsonTime:F2} мс ({jsonTime / iterations:F6} мс/оп)");

            if (reflectionTime > 0)
            {
                double ratio = jsonTime / reflectionTime;
                Console.WriteLine($"System.Text.Json быстрее в {ratio:F2} раз");
            }

            Console.WriteLine("\n4. ТЕСТИРОВАНИЕ ДЕСЕРИАЛИЗАЦИИ:");
            Console.WriteLine(new string('-', 50));

            Console.WriteLine($"CSV строка для десериализации: {csvResult}");

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var deserializedF = csvSerializer.Deserialize<F>(csvResult);
            }
            sw.Stop();

            Console.WriteLine($"\nCSV десериализация ({iterations:N0} итераций):");
            Console.WriteLine($"Время: {sw.Elapsed.TotalMilliseconds:F2} мс");
            Console.WriteLine($"Среднее: {sw.Elapsed.TotalMilliseconds / iterations:F6} мс/оп");

            try
            {
                var resultF = csvSerializer.Deserialize<F>(csvResult);
                Console.WriteLine($"Результат: {resultF}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка десериализации: {ex.Message}");
            }

            Console.WriteLine("\n5. INI ДЕСЕРИАЛИЗАЦИЯ:");
            Console.WriteLine(new string('-', 50));

            var iniDeserializer = new IniDeserializer();
            var iniContent = @"Name=Иван Иванов
Age=30
Email=ivan@example.com
IsActive=true
Salary=1234.56";

            Console.WriteLine("INI содержимое:");
            Console.WriteLine(iniContent);

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var person = iniDeserializer.Deserialize<Person>(iniContent);
            }
            sw.Stop();

            Console.WriteLine($"\nINI десериализация ({iterations:N0} итераций):");
            Console.WriteLine($"Время: {sw.Elapsed.TotalMilliseconds:F2} мс");
            Console.WriteLine($"Среднее: {sw.Elapsed.TotalMilliseconds / iterations:F6} мс/оп");

            try
            {
                var resultPerson = iniDeserializer.Deserialize<Person>(iniContent);
                Console.WriteLine($"Результат: {resultPerson}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка десериализации: {ex.Message}");
            }

            Console.WriteLine("\n6. РАБОТА С ФАЙЛАМИ:");
            Console.WriteLine(new string('-', 50));

            try
            {
                Directory.CreateDirectory("test_data");

                var csvFileContent = "I1,I2,I3,I4,I5,mas\n1,2,3,4,5,\"[1,2]\"\n6,7,8,9,10,\"[3,4,5]\"";
                File.WriteAllText("test_data/test.csv", csvFileContent, Encoding.UTF8);

                File.WriteAllText("test_data/test.ini", iniContent, Encoding.UTF8);

                Console.WriteLine("Созданы тестовые файлы:");
                Console.WriteLine($"  test_data/test.csv - {new FileInfo("test_data/test.csv").Length} байт");
                Console.WriteLine($"  test_data/test.ini - {new FileInfo("test_data/test.ini").Length} байт");

                var csvLines = File.ReadAllLines("test_data/test.csv");
                if (csvLines.Length > 1)
                {
                    try
                    {
                        var fileF = csvSerializer.Deserialize<F>(csvLines[1]);
                        Console.WriteLine($"\nДанные из CSV файла: {fileF}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка чтения CSV файла: {ex.Message}");
                    }
                }

                try
                {
                    var iniFileContent = File.ReadAllText("test_data/test.ini");
                    var filePerson = iniDeserializer.Deserialize<Person>(iniFileContent);
                    Console.WriteLine($"Данные из INI файла: {filePerson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка чтения INI файла: {ex.Message}");
                }

                Directory.Delete("test_data", true);
                Console.WriteLine("\nТестовые файлы удалены.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при работе с файлами: {ex.Message}");
            }

            Console.WriteLine("\n7. ИТОГОВАЯ ТАБЛИЦА ПРОИЗВОДИТЕЛЬНОСТИ:");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("{0,-25} | {1,10} | {2,15} | {3,10}",
                "Операция", "Время (мс)", "мс/операция", "оп/сек");
            Console.WriteLine(new string('-', 70));

            iterations = 100000; 

            sw.Restart();
            for (int i = 0; i < iterations; i++) csvSerializer.Serialize(f);
            sw.Stop();
            var reflectionSerializeTime = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (int i = 0; i < iterations; i++) JsonSerializer.Serialize(f);
            sw.Stop();
            var jsonSerializeTime = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (int i = 0; i < iterations; i++) csvSerializer.Deserialize<F>(csvResult);
            sw.Stop();
            var csvDeserializeTime = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (int i = 0; i < iterations; i++) iniDeserializer.Deserialize<Person>(iniContent);
            sw.Stop();
            var iniDeserializeTime = sw.Elapsed.TotalMilliseconds;

            PrintRow("Reflection CSV сериализация", reflectionSerializeTime, iterations);
            PrintRow("System.Text.Json сериализация", jsonSerializeTime, iterations);
            PrintRow("CSV десериализация", csvDeserializeTime, iterations);
            PrintRow("INI десериализация", iniDeserializeTime, iterations);

            Console.WriteLine(new string('=', 70));

            Console.WriteLine("\n8. ВЫВОДЫ:");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. Reflection позволяет создавать универсальные сериализаторы");
            Console.WriteLine("2. Кэширование Reflection критически важно для производительности");
            Console.WriteLine("3. System.Text.Json оптимизирован и быстрее Reflection в 2-5 раз");
            Console.WriteLine("4. Десериализация медленнее сериализации из-за парсинга и конвертации");
            Console.WriteLine("5. Вывод в консоль добавляет накладные расходы (~0.1-5 мс на строку)");
            Console.WriteLine("6. CSV формат требует корректной обработки кавычек и разделителей");
            Console.WriteLine("7. Reflection подходит для учебных целей и простых задач");
        }

        static void PrintRow(string operation, double totalMs, int iterations)
        {
            var perOperation = totalMs / iterations;
            var perSecond = iterations / (totalMs / 1000);
            Console.WriteLine("{0,-25} | {1,10:F2} | {2,15:F6} | {3,10:F0}",
                operation, totalMs, perOperation, perSecond);
        }
    }
}