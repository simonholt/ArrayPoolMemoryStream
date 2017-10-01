# ArrayPoolMemoryStream
An experimental replacement for MemoryStream using ArrayPool

**This is not production-quality code, and is only used for benchmarking purposes!**

Lots of serializers use code of this form:
```
using (var ms = new MemoryStream())
{
   mySerializer(myObject, ms);
   return ms.ToArray();   
}
```

This can lead to a lot of intermediate garbage being generated if the object is large.

With the introduction of ArrayPool in System.Buffers, I thought I'd knock together a MemoryStream implementation to see how it compares.


There is a fully-implemented version here:

https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream

However, this uses its own pool rather than System.Buffers.ArrayPool.

