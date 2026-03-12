using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardDatabase))]
public class CardDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        CardDatabase database = (CardDatabase)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Сохранить в JSON"))
        {
            database.SaveToJSON("cards.json");
            Debug.Log("Карты сохранены в cards.json");
        }
    }
}