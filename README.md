# Simple file storage solution

This package allows for data storage using serialization. It utilises a JSON-based serialisation to record data in .jk files (it's a json and it has a key, .jk is not a joke).

It is a generics-based solution that receives a key that will be used as file name and an <T> object. It utilises the input class structure, so no manual intervention is needed - it'll work with any given object.

## Functionalities:
1. *void Insert(string key, T obj):* Allows insertion of a new record. The 'string key' parameter will be used as file name and T object is the data class.
2. *T Get<T>(string key)*: Searches a file by name and, if possible, will deserialise to a given T object.
3, *IEnumerable<string> GetAllKeys()*: This method searches for all .jk files and returns all entries.
