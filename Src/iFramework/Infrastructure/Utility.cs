﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web;
using System.Xml.Serialization;
using System.Xml;
using Newtonsoft.Json.Linq;
using AutoMapper;
using System.Linq.Expressions;
using System.Reflection;
using System.ServiceModel;
using IFramework.Infrastructure.Unity.LifetimeManagers;
using Microsoft.Practices.Unity;
using System.Dynamic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json;

namespace IFramework.Infrastructure
{
    public class QueryParameter
    {
        private string name = null;
        private string value = null;

        public QueryParameter(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public string Name
        {
            get { return name; }
        }

        public string Value
        {
            get { return value; }
            set { this.value = value; }
        }
    }

    public class AutoMapForMemberOption<TSource, TDestination>
        where TSource : class
        where TDestination : class
    {
        public AutoMapForMemberOption()
        {

        }
        public AutoMapForMemberOption(Expression<Func<TDestination, object>> destinationMember,
            Action<IMemberConfigurationExpression<TSource>> memberOptions)
        {
            DestinationMember = destinationMember;
            MemberOptions = memberOptions;
        }

        public Expression<Func<TDestination, object>> DestinationMember { get; set; }
        public Action<IMemberConfigurationExpression<TSource>> MemberOptions { get; set; }
    }

    public sealed class QueryStringHelper
    {
        private QueryStringHelper() { }
        private static string[] SplitAt(string s, string delimiter)
        {
            if (delimiter == null ||
               delimiter.Length == 0) throw new ArgumentException();
            if (s == null || s.Length == 0) return new string[] { "" };
            int index = 0;
            bool first = true;
            List<string> strings = null;
            int delimLength = delimiter.Length;
            while (true)
            {
                int index2 = s.IndexOf(delimiter, index, StringComparison.Ordinal);
                if (index2 < 0)
                {
                    if (first) return new string[] { s };
                    strings.Add(s.Substring(index));
                    break;
                }
                else
                {
                    if (first)
                    {
                        strings = new List<string>();
                        first = false;
                    }
                    string newstr = s.Substring(index, (index2) - (index));
                    strings.Add(newstr);
                    index = index2 + delimLength;
                }
            }
            return strings.ToArray();
        }

        private static int ToHexNumber(int c)
        {
            if (c >= 'A' && c <= 'Z')
                return 10 + c - 'A';
            else if (c >= 'a' && c <= 'z')
                return 10 + c - 'a';
            else if (c >= '0' && c <= '9')
                return c - '0';
            return -1;
        }
        private static string PercentDecodeUTF8(string str)
        {
            int len = str.Length;
            bool percent = false;
            for (int i = 0; i < len; i++)
            {
                char c = str[i];
                if (c == '%')
                {
                    percent = true;
                }
                else if (c >= 0x80) // Non-ASCII characters not allowed
                    return null;
            }
            if (!percent) return str;// return early if there are no percent decodings
            int cp = 0;
            int bytesSeen = 0;
            int bytesNeeded = 0;
            int lower = 0x80;
            int upper = 0xBF;
            int markedPos = -1;
            StringBuilder retString = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                int c = str[i];
                if (c == '%')
                {
                    if (i + 2 < len)
                    {
                        int a = ToHexNumber(str[i + 1]);
                        int b = ToHexNumber(str[i + 2]);
                        if (a >= 0 && b >= 0)
                        {
                            b = (byte)(a * 16 + b);
                            i += 2;
                            // b now contains the byte read
                            if (bytesNeeded == 0)
                            {
                                // this is the lead byte
                                if (b < 0x80)
                                {
                                    retString.Append((char)b);
                                    continue;
                                }
                                else if (b >= 0xc2 && b <= 0xdf)
                                {
                                    markedPos = i;
                                    bytesNeeded = 1;
                                    cp = b - 0xc0;
                                }
                                else if (b >= 0xe0 && b <= 0xef)
                                {
                                    markedPos = i;
                                    lower = (b == 0xe0) ? 0xa0 : 0x80;
                                    upper = (b == 0xed) ? 0x9f : 0xbf;
                                    bytesNeeded = 2;
                                    cp = b - 0xe0;
                                }
                                else if (b >= 0xf0 && b <= 0xf4)
                                {
                                    markedPos = i;
                                    lower = (b == 0xf0) ? 0x90 : 0x80;
                                    upper = (b == 0xf4) ? 0x8f : 0xbf;
                                    bytesNeeded = 3;
                                    cp = b - 0xf0;
                                }
                                else
                                {
                                    // illegal byte in UTF-8
                                    retString.Append('\uFFFD');
                                    continue;
                                }
                                cp <<= (6 * bytesNeeded);
                                continue;
                            }
                            else
                            {
                                // this is a second or further byte
                                if (b < lower || b > upper)
                                {
                                    // illegal trailing byte
                                    cp = bytesNeeded = bytesSeen = 0;
                                    lower = 0x80;
                                    upper = 0xbf;
                                    i = markedPos; // reset to the last marked position
                                    retString.Append('\uFFFD');
                                    continue;
                                }
                                // reset lower and upper for the third
                                // and further bytes
                                lower = 0x80;
                                upper = 0xbf;
                                bytesSeen++;
                                cp += (b - 0x80) << (6 * (bytesNeeded - bytesSeen));
                                markedPos = i;
                                if (bytesSeen != bytesNeeded)
                                {
                                    // continue if not all bytes needed
                                    // were read yet
                                    continue;
                                }
                                int ret = cp;
                                cp = 0;
                                bytesSeen = 0;
                                bytesNeeded = 0;
                                // append the Unicode character
                                if (ret <= 0xFFFF) { retString.Append((char)(ret)); }
                                else
                                {
                                    retString.Append((char)((((ret - 0x10000) >> 10) & 0x3FF) + 0xD800));
                                    retString.Append((char)((((ret - 0x10000)) & 0x3FF) + 0xDC00));
                                }
                                continue;
                            }
                        }
                    }
                }
                if (bytesNeeded > 0)
                {
                    // we expected further bytes here,
                    // so emit a replacement character instead
                    bytesNeeded = 0;
                    retString.Append('\uFFFD');
                }
                // append the code point as is (we already
                // checked for ASCII characters so this will
                // be simple
                retString.Append((char)(c & 0xFF));
            }
            if (bytesNeeded > 0)
            {
                // we expected further bytes here,
                // so emit a replacement character instead
                bytesNeeded = 0;
                retString.Append('\uFFFD');
            }
            return retString.ToString();
        }
        public static IList<string[]> ParseQueryString(
            string input
        )
        {
            return ParseQueryString(input, null);
        }

        public static IList<string[]> ParseQueryString(
            string input, string delimiter
        )
        {
            if ((input) == null) throw new ArgumentNullException("input");
            if (delimiter == null)
            {
                // set default delimiter to ampersand
                delimiter = "&";
            }
            // Check input for non-ASCII characters
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] > 0x7F)
                    throw new ArgumentException("input contains a non-ASCII character");
            }
            // split on delimiter
            string[] strings = SplitAt(input, delimiter);
            List<string[]> pairs = new List<string[]>();
            foreach (var str in strings)
            {
                if (str.Length == 0)
                {
                    continue;
                }
                // split on key
                int index = str.IndexOf('=');
                string name = str;
                string value = "";// value is empty if there is no key
                if (index >= 0)
                {
                    name = str.Substring(0, (index) - (0));
                    value = str.Substring(index + 1);
                }
                name = name.Replace('+', ' ');
                value = value.Replace('+', ' ');
                string[] pair = new string[] { name, value };
                pairs.Add(pair);
            }
            foreach (var pair in pairs)
            {
                // percent decode the key and value if necessary
                pair[0] = PercentDecodeUTF8(pair[0]);
                pair[1] = PercentDecodeUTF8(pair[1]);
            }
            return pairs;
        }

        private static string[] GetKeyPath(string s)
        {
            int index = s.IndexOf('[');
            if (index < 0)
            {// start bracket not found
                return new string[] { s };
            }
            List<string> path = new List<string>();
            path.Add(s.Substring(0, (index) - (0)));
            index++;// move to after the bracket
            while (true)
            {
                int endBracket = s.IndexOf(']', index);
                if (endBracket < 0)
                { // end bracket not found
                    path.Add(s.Substring(index));
                    break;
                }
                path.Add(s.Substring(index, (endBracket) - (index)));
                index = endBracket + 1; // move to after the end bracket
                index = s.IndexOf('[', index);
                if (index < 0)
                {// start bracket not found
                    break;
                }
                index++;// move to after the start bracket
            }
            return path.ToArray();
        }

        private static bool IsList(IDictionary<string, Object> dict)
        {
            if (dict == null) return false;
            int index = 0;
            int count = dict.Count;
            if (count == 0) return false;
            while (true)
            {
                if (index == count)
                    return true;
                string indexString = Convert.ToString(index, CultureInfo.InvariantCulture);
                if (!dict.ContainsKey(indexString))
                {
                    return false;
                }
                index++;
            }
        }
        private static IList<Object> ConvertToList(IDictionary<string, Object> dict)
        {
            List<Object> ret = new List<Object>();
            int index = 0;
            int count = dict.Count;
            while (index < count)
            {
                string indexString = Convert.ToString(index, CultureInfo.InvariantCulture);
                ret.Add(dict[indexString]);
                index++;
            }
            return ret;
        }

        private static void ConvertLists(IList<Object> dict)
        {
            for (int i = 0; i < dict.Count; i++)
            {

                IDictionary<string, Object> value = ((dict[i] is IDictionary<string, Object>) ? (IDictionary<string, Object>)dict[i] : null);
                // A list contains only indexes 0, 1, 2, and so on,
                // with no gaps.
                if (IsList(value))
                {
                    IList<Object> newList = ConvertToList(value);
                    dict[i] = newList;
                    ConvertLists(newList);
                }
                else if (value != null)
                {
                    // Convert the list's descendents
                    // if they are lists
                    ConvertLists(value);
                }
            }
        }

        private static void ConvertLists(IDictionary<string, Object> dict)
        {
            foreach (var key in new List<string>(dict.Keys))
            {

                IDictionary<string, Object> value = ((dict[key] is IDictionary<string, Object>) ? (IDictionary<string, Object>)dict[key] : null);
                // A list contains only indexes 0, 1, 2, and so on,
                // with no gaps.
                if (IsList(value))
                {
                    IList<Object> newList = ConvertToList(value);
                    dict.Add(key, newList);
                    ConvertLists(newList);
                }
                else if (value != null)
                {
                    // Convert the dictionary's descendents
                    // if they are lists
                    ConvertLists(value);
                }
            }
        }

        public static IDictionary<string, Object> QueryStringToDict(string query)
        {
            return QueryStringToDict(query, "&");
        }

        public static IDictionary<string, Object> QueryStringToDict(string query, string delimiter)
        {
            IDictionary<string, Object> root = new Dictionary<string, Object>();
            foreach (var keyvalue in ParseQueryString(query, delimiter))
            {
                string[] path = GetKeyPath(keyvalue[0]);
                IDictionary<string, Object> leaf = root;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    if (!leaf.ContainsKey(path[i]))
                    {
                        // node doesn't exist so add it
                        IDictionary<string, Object> newLeaf = new Dictionary<string, Object>();
                        leaf.Add(path[i], newLeaf);
                        leaf = newLeaf;
                    }
                    else
                    {

                        IDictionary<string, Object> o = ((leaf[path[i]] is IDictionary<string, Object>) ? (IDictionary<string, Object>)leaf[path[i]] : null);
                        if (o != null)
                        {
                            leaf = o;
                        }
                        else
                        {
                            // error, not a dictionary
                            leaf = null;
                            break;
                        }
                    }
                }
                if (leaf != null)
                {
                    leaf.Add(path[path.Length - 1], keyvalue[1]);
                }
            }
            // Convert array-like dictionaries to ILists
            ConvertLists(root);
            return root;
        }
    }


    public static class Utility
    {
        public static bool TryDo(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool TryRemove(this Hashtable hashtable, object key)
        {
            return TryDo(() => hashtable.Remove(key));
        }

        public static bool TryRemove(this IDictionary collection, object key)
        {
            return TryDo(() => collection.Remove(key));
        }
        public static object InvokeGenericMethod(this object obj, Type genericType, string method, object[] args)
        {
            MethodInfo mi = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First(m => m.Name == method && m.IsGenericMethod);
            MethodInfo miConstructed = mi.MakeGenericMethod(genericType);
            FastInvoke.FastInvokeHandler fastInvoker = FastInvoke.GetMethodInvoker(miConstructed);
            return fastInvoker(obj, args);
        }

        public static object InvokeMethod(this object obj, string method, object[] args)
        {
            MethodInfo mi = null;
            foreach (var m in obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == method && m.GetParameters().Length == args.Length)
                {
                    bool equalParameters = true;
                    for (int i = 0; i < m.GetParameters().Length; i++)
                    {
                        var type = m.GetParameters()[i];
                        if (!type.ParameterType.IsInstanceOfType(args[i]))
                        {
                            equalParameters = false;
                            break;
                        }
                    }
                    if (equalParameters)
                    {
                        mi = m;
                        break;
                    }
                }
            }
            if (mi == null)
            {
                throw new NotSupportedException();
            }
            FastInvoke.FastInvokeHandler fastInvoker = FastInvoke.GetMethodInvoker(mi);
            return fastInvoker(obj, args);
        }


        //public static TRole ActAs<TRole>(this IAggregateRoot entity)
        //    where TRole : Framework.DCI.IRole
        //{
        //    TRole role = IoCFactory.Resolve<TRole>(new ParameterOverride("actor", entity));
        //    return role;
        //}

        public static TAttribute GetCustomAttribute<TAttribute>(this object obj, bool inherit = true)
        where TAttribute : Attribute
        {
            if (obj is Type)
            {
                var attrs = (obj as Type).GetCustomAttributes(typeof(TAttribute), inherit);
                if (attrs != null)
                {
                    return attrs.FirstOrDefault() as TAttribute;
                }
            }
            else if (obj is PropertyInfo)
            {
                var attrs = ((PropertyInfo)obj).GetCustomAttributes(inherit);
                if (attrs != null && attrs.Length > 0)
                {
                    return attrs.FirstOrDefault(attr => attr is TAttribute) as TAttribute;
                }
            }
            else if (obj is MethodInfo)
            {
                var attrs = (obj as MethodInfo).GetCustomAttributes(inherit);
                if (attrs != null && attrs.Length > 0)
                {
                    return attrs.FirstOrDefault(attr => attr is TAttribute) as TAttribute;
                }
            }
            else if (obj.GetType().IsDefined(typeof(TAttribute), true))
            {
                var attr = Attribute.GetCustomAttribute(obj.GetType(), typeof(TAttribute), inherit) as TAttribute;
                return attr;
            }
            return null;
        }

        public static IEnumerable<T> ForEach<T>(
                this IEnumerable<T> source,
                Action<T> act)
        {
            if (source != null)
            {
                foreach (T element in source) act(element);
            }
            return source;
        }

        public static void DisposeContainerExtensions()
        {
            if (OperationContext.Current != null)
            {
                OperationContext.Current.Extensions.OfType<ContainerExtension>().ForEach<ContainerExtension>(
                ce =>
                {
                    var idisposable = ce.Value as IDisposable;
                    if (idisposable != null)
                    {
                        idisposable.Dispose();
                    }

                }
                );
            }
        }

        public static TDestination ToMapObject<TDestination>
          (this object src)
            where TDestination : class
        {
            var mapper = Mapper.CreateMap(src.GetType(), typeof(TDestination));
            var dest = Mapper.Map(src, src.GetType(), typeof(TDestination)) as TDestination;
            return dest;
        }

        public static TDestination ToMapObject<TSource, TDestination>
           (this TSource src,
           params AutoMapForMemberOption<TSource, TDestination>[] memberOptions)
            where TSource : class
            where TDestination : class
        {
            return ToMapObject(src, null, null, memberOptions);
        }

        public static TDestination ToMapObject<TSource, TDestination>
           (this TSource src, TDestination dest,
           params AutoMapForMemberOption<TSource, TDestination>[] memberOptions)
            where TSource : class
            where TDestination : class
        {
            return ToMapObject(src, dest, null, memberOptions);
        }

        public static TDestination ToMapObject<TSource, TDestination>
          (this TSource src, TDestination dest,
          params Expression<Func<TDestination, object>>[] ignoreMembers)
            where TSource : class
            where TDestination : class
        {
            return ToMapObject(src, dest, ignoreMembers, null);
        }

        public static TDestination ToMapObject<TSource, TDestination>
            (this TSource src, TDestination dest,
            IEnumerable<Expression<Func<TDestination, object>>> ignoreMembers,
            AutoMapForMemberOption<TSource, TDestination>[] memberOptions)
            where TSource : class
            where TDestination : class
        {
            var mapper = Mapper.CreateMap<TSource, TDestination>();

            if (ignoreMembers != null && ignoreMembers.Count() > 0)
            {
                ignoreMembers.ForEach(ignore =>
                {
                    mapper.ForMember(ignore, opt => opt.Ignore());
                });
            }

            if (memberOptions != null && memberOptions.Length > 0)
            {
                memberOptions.ForEach(mo =>
                {
                    mapper.ForMember(mo.DestinationMember, mo.MemberOptions);
                });
            }
            if (dest == null)
            {
                dest = Mapper.Map<TSource, TDestination>(src) as TDestination;
            }
            else
            {
                dest = Mapper.Map(src, dest) as TDestination;
            }
            return dest;
        }

        //public static object ToMapObject(this object src, object dest)
        //{
        //    Mapper.CreateMap(src.GetType(), dest.GetType());
        //    if (dest == null)
        //    {
        //        dest = Mapper.Map(src, src.GetType(), dest.GetType());
        //    }
        //    else
        //    {
        //        dest = Mapper.Map(src, dest, src.GetType(), dest.GetType());
        //    }
        //    return dest;
        //}

        public static TDestination CreateObject<TSource, TDestination>(TSource obj)
        {
            Mapper.CreateMap<TSource, TDestination>();
            var dest = Mapper.Map<TSource, TDestination>(obj);
            return dest;
        }

        public static void UpdateObject<TSource, TDestination>(TSource obj, TDestination destObj)
        {
            Mapper.CreateMap<TSource, TDestination>();
            Mapper.Map<TSource, TDestination>(obj, destObj);
        }

        public static void UpdateObject(dynamic srcObj, dynamic destObj)
        {
            Mapper.CreateMap(srcObj.GetType(), destObj.GetType());
            Mapper.Map(srcObj, destObj, srcObj.GetType(), destObj.GetType());
        }

        public static string GetTimeToString(DateTime datetime, bool isEnglish)
        {
            string lang = isEnglish ? "en-US" : "zh-CN";
            string timetext = string.Empty;
            TimeSpan span = DateTime.Now - datetime;
            if (span.Days > 30)
            {
                timetext = datetime.ToShortDateString();
            }
            else if (span.Days >= 1)
            {
                timetext = string.Format("{0}{1}", span.Days, GetResource("Day", lang));
            }
            else if (span.Hours >= 1)
            {
                timetext = string.Format("{0}{1}", span.Hours, GetResource("Hour", lang));
            }
            else if (span.Minutes >= 1)
            {
                timetext = string.Format("{0}{1}", span.Minutes, GetResource("Minute", lang));
            }
            else if (span.Seconds >= 1)
            {
                timetext = string.Format("{0}{1}", span.Seconds, GetResource("Second", lang));
            }
            else
            {
                timetext = string.Format("1{0}", GetResource("Second", lang));
            }
            return timetext;
        }

        public static IQueryable<T> GetPageElements<T>(this IQueryable<T> query, int pageIndex, int pageSize)
        {
            return query.Skip(pageIndex * pageSize).Take(pageSize);
        }
        internal static string GetUniqueIdentifier(int length)
        {
            try
            {
                int maxSize = length;
                char[] chars = new char[62];
                string a;
                a = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
                chars = a.ToCharArray();
                int size = maxSize;
                byte[] data = new byte[1];
                RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
                crypto.GetNonZeroBytes(data);
                size = maxSize;
                data = new byte[size];
                crypto.GetNonZeroBytes(data);
                StringBuilder result = new StringBuilder(size);
                foreach (byte b in data)
                {
                    result.Append(chars[b % (chars.Length - 1)]);
                }
                // Unique identifiers cannot begin with 0-9
                if (result[0] >= '0' && result[0] <= '9')
                {
                    return GetUniqueIdentifier(length);
                }
                return result.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GENERATE_UID_FAIL", ex);
            }
        }

        public static T GetValueByKey<T>(this object obj, string name)
        {
            T retValue = default(T);
            object objValue = null;
            try
            {
                if (obj is Newtonsoft.Json.Linq.JObject)
                {
                    var jObject = obj as Newtonsoft.Json.Linq.JObject;
                    var property = jObject.Property(name);
                    if (property != null)
                    {
                        var value = property.Value as Newtonsoft.Json.Linq.JValue;
                        if (value != null)
                        {
                            objValue = value.Value;
                        }
                    }
                }
                else
                {
                    var property = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null)
                    {
                        objValue = FastInvoke.GetMethodInvoker(property.GetGetMethod(true))(obj, null);
                        //Func<T> PGet = Delegate.CreateDelegate(typeof(Func<T>), obj, property.GetGetMethod(true)) as Func<T>;
                        //objValue = PGet();
                        // property.GetValue(obj, null);
                    }
                }

                if (objValue != null)
                {
                    retValue = (T)objValue;
                }
            }
            catch (System.Exception)
            {
                retValue = default(T);
            }
            return retValue;
        }

        public static object GetValueByKey(this object obj, string name)
        {
            object objValue = null;
            if (obj is Newtonsoft.Json.Linq.JObject)
            {
                var jObject = obj as Newtonsoft.Json.Linq.JObject;
                var property = jObject.Property(name);
                if (property != null)
                {
                    var value = property.Value as Newtonsoft.Json.Linq.JValue;
                    if (value != null)
                    {
                        objValue = value.Value;
                    }
                }
            }
            else
            {
                var property = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    objValue = FastInvoke.GetMethodInvoker(property.GetGetMethod(true))(obj, null);
                    // objValue = property.GetValue(obj, null);
                }
            }
            return objValue;
        }

        public static void SetValueByKey(this object obj, string name, object value)
        {
            if (obj is DynamicJson)
            {
                obj = (obj as DynamicJson)._json;
            }
            if (obj is Newtonsoft.Json.Linq.JObject)
            {
                var jObject = obj as Newtonsoft.Json.Linq.JObject;
                var property = jObject.Property(name);
                if (property != null)
                {
                    property.Value = JToken.FromObject(value);
                }
                else
                {
                    jObject.Add(name, JToken.FromObject(value));
                }
            }
            else
            {
                var property = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    FastInvoke.GetMethodInvoker(property.GetSetMethod(true))(obj, new object[] { value });
                }
            }
        }

        public static T ToEnum<T>(this string val)
        {
            return ParseEnum<T>(val);
        }

        public static T ParseEnum<T>(string val)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), val);
            }
            catch (System.Exception)
            {
                return default(T);
            }
        }

        public static LambdaExpression GetLambdaExpression(Type type, string propertyName)
        {
            ParameterExpression param = Expression.Parameter(type);
            PropertyInfo property = type.GetProperty(propertyName);
            Expression propertyAccessExpression = Expression.MakeMemberAccess(param, property);
            var le = Expression.Lambda(propertyAccessExpression, param);
            return le;
        }

        public static IQueryable<TEntity> GetOrderByQueryable<TEntity>(IQueryable<TEntity> query, LambdaExpression orderByExpression, bool asc)
            where TEntity : class
        {
            var orderBy = asc ? "OrderBy" : "OrderByDescending";
            MethodCallExpression orderByCallExpression =
                        Expression.Call(typeof(Queryable),
                        orderBy,
                        new Type[] { typeof(TEntity),
                        orderByExpression.Body.Type},
                        query.Expression,
                        orderByExpression);
            return query.Provider.CreateQuery<TEntity>(orderByCallExpression);
        }


        public static List<QueryParameter> GetQueryParameters(string parameters)
        {
            if (parameters.StartsWith("?"))
            {
                parameters = parameters.Remove(0, 1);
            }

            List<QueryParameter> result = new List<QueryParameter>();

            if (!string.IsNullOrEmpty(parameters))
            {
                string[] p = parameters.Split('&');
                foreach (string s in p)
                {
                    if (!string.IsNullOrEmpty(s))
                    {
                        if (s.IndexOf('=') > -1)
                        {
                            string[] temp = s.Split('=');
                            result.Add(new QueryParameter(temp[0], temp[1]));
                        }
                        else
                        {
                            result.Add(new QueryParameter(s, string.Empty));
                        }
                    }
                }
            }

            return result;
        }

        public static string NormalizeRequestParameters(IList<QueryParameter> parameters)
        {
            StringBuilder sb = new StringBuilder();
            QueryParameter p = null;
            for (int i = 0; i < parameters.Count; i++)
            {
                p = parameters[i];
                sb.AppendFormat("{0}={1}", p.Name, p.Value);

                if (i < parameters.Count - 1)
                {
                    sb.Append("&");
                }
            }

            return sb.ToString();
        }

        //加密算法

        public static string MD5Encrypt(string pToEncrypt, CipherMode mode = CipherMode.CBC, string key = "IVANIVAN")
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Mode = mode;
            byte[] inputByteArray = Encoding.Default.GetBytes(pToEncrypt);
            des.Key = ASCIIEncoding.ASCII.GetBytes(key);
            des.IV = ASCIIEncoding.ASCII.GetBytes(key);
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            StringBuilder ret = new StringBuilder();
            foreach (byte b in ms.ToArray())
            {
                ret.AppendFormat("{0:X2}", b);
            }
            ret.ToString();
            return ret.ToString();

        }

        public static string MD5Decrypt(string pToDecrypt, CipherMode mode = CipherMode.CBC, string key = "IVANIVAN")
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Mode = mode;
            byte[] inputByteArray = new byte[pToDecrypt.Length / 2];
            for (int x = 0; x < pToDecrypt.Length / 2; x++)
            {
                int i = (Convert.ToInt32(pToDecrypt.Substring(x * 2, 2), 16));
                inputByteArray[x] = (byte)i;
            }
            des.Key = ASCIIEncoding.ASCII.GetBytes(key);
            des.IV = ASCIIEncoding.ASCII.GetBytes(key);

            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            StringBuilder ret = new StringBuilder();
            return System.Text.Encoding.ASCII.GetString(ms.ToArray());

        }

        public static Exception GetRescureInnerException(this Exception ex)
        {
            var innerEx = ex;
            while (innerEx.InnerException != null)
            {
                innerEx = innerEx.InnerException;
            }
            return innerEx;
        }

        public static bool IsGuid(string id)
        {
            bool flag = true;
            try
            {
                new Guid(id.Trim());
            }
            catch (Exception)
            {
                flag = false;
            }
            return flag;
        }

        public static string GetLocalResource(string path, string key, string lang)
        {
            object resource = string.Empty;
            if (!string.IsNullOrEmpty(lang))
            {
                resource = HttpContext.GetLocalResourceObject(path, key, new System.Globalization.CultureInfo(lang));
            }
            else
            {
                resource = HttpContext.GetLocalResourceObject(path, key);
            }
            if (resource != null)
            {
                return resource.ToString();
            }
            return string.Empty;
        }

        public static string GetLocalResource(string path, string key)
        {
            return GetLocalResource(path, key, string.Empty);
        }

        public static string GetResource(string key, string lang)
        {
            object resource = string.Empty;
            if (!string.IsNullOrEmpty(lang))
            {
                resource = HttpContext.GetGlobalResourceObject("GlobalResource", key, new System.Globalization.CultureInfo(lang));
            }
            else
            {
                resource = HttpContext.GetGlobalResourceObject("GlobalResource", key);
            }
            if (resource != null)
            {
                return resource.ToString();
            }
            return string.Empty;
        }


        public static string GetResource(string key)
        {
            return GetResource(key, string.Empty);
        }

        public static string StyledSheetEncode(string s)
        {
            s = s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n\r", "\\n").Replace("\r", "\\n").Replace("\n", "\\n");
            s = s.Replace("/", "\\/");
            return s;
        }

        public static string GetMd5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        static public string Serialize(object xmlContent, Encoding encoding = null)
        {
            System.Xml.Serialization.XmlSerializer serializer = new XmlSerializer(xmlContent.GetType());
            //StringBuilder builder = new System.Text.StringBuilder();
            //StringWriter writer = new StringWriterWithEncoding(Encoding.UTF8);
            //new System.IO.StringWriter(builder);
            //System.Xml.XmlTextWriter writer = new System.Xml.XmlTextWriter(@"c:\test.xml", System.Text.Encoding.UTF8);
            //serializer.Serialize(writer, xmlContent);
            //return builder.ToString();

            MemoryStream stream = new MemoryStream();
            XmlWriterSettings setting = new XmlWriterSettings();
            setting.Encoding = encoding ?? Encoding.GetEncoding("utf-8");
            setting.Indent = true;
            using (XmlWriter writer = XmlWriter.Create(stream, setting))
            {
                serializer.Serialize(writer, xmlContent);
            }
            return System.Text.RegularExpressions.Regex.Replace(Encoding.GetEncoding("utf-8").GetString(stream.ToArray()), "^[^<]", "");
        }

        static public object DeSerialize<XmlType>(string xmlString)
        {

            System.Xml.Serialization.XmlSerializer serializer = new XmlSerializer(typeof(XmlType));
            StringBuilder builder = new StringBuilder(xmlString);
            System.IO.StringReader reader = new System.IO.StringReader(builder.ToString());
            try
            {
                return serializer.Deserialize(reader);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Func<TObject, TProperty> GetFieldValueExp<TObject, TProperty>(string fieldName)
        {
            var paramExpr = Expression.Parameter(typeof(TObject));
            var propOrFieldVisit = Expression.PropertyOrField(paramExpr, fieldName);
            var lambda = Expression.Lambda<Func<TObject, TProperty>>(propOrFieldVisit, paramExpr);
            return lambda.Compile();
        }

        /// <summary> 
        /// 序列化 
        /// </summary> 
        /// <param name="data">要序列化的对象</param> 
        /// <returns>返回存放序列化后的数据缓冲区</returns> 
        public static byte[] ToBytes(this object data)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream rems = new MemoryStream();
            formatter.Serialize(rems, data);
            return rems.GetBuffer();
        }

        /// <summary> 
        /// 反序列化 
        /// </summary> 
        /// <param name="data">数据缓冲区</param> 
        /// <returns>对象</returns> 
        public static object ToObject(this byte[] data)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream rems = new MemoryStream(data);
            data = null;
            return formatter.Deserialize(rems);
        }
    }
}
