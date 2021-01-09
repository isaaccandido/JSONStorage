# **Simple file storage solution**

This package allows for data storage using serialization. It utilises a binary JSON-based serialisation to record data in .j2k files (it's a json and it has a key, .j2k is not a joke).


It is a generics-based solution that receives a key that will be used as file name and an <T> object. It utilises the input class structure, so no manual intervention is needed - it'll work with any given object.

### **Functionalities:**
1. *void Insert(string key, T obj):* Allows insertion of a new record. The 'string key' parameter will be used as file name and T object is the data class.
2. *T Get<T>(string key)*: Searches a file by name and, if possible, will deserialise to a given T object.
3. *IEnumerable<string> GetAllKeys()*: This method searches for all .jk files and returns all entries.

---

## **IMPORTANT NOTE!**
From version 0.3 and above, this solution is BSON-based (binary JSON). Nice, huh? It's faster to serialise/deserialise. Also, smaller! The way I see it, it's a win-win, that's why I did it. 

Therefore, from now on, files will be saved as .j2k (it used to be *a* joke, now it's two). - The file extension now is .j2k. 

Last, but not least: for those who already use 0.2.x versions of this solution, fear not! I took my sweet time to automatically convert your JSON files (.jk) into BSON binary files (.j2k) upon booting up on a given directory. Additionally, it'll keep a *.legacy* file in there, containing old .jk files ("*[fileName.jk.legacy]*"). No files need to be deleted.

I know, I know, it's ok. You're welcome. 