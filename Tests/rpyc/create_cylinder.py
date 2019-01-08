import unity_connection
UnityEngine = unity_connection.get_module('UnityEngine')

go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cylinder);
go.name = "myCylinder";
