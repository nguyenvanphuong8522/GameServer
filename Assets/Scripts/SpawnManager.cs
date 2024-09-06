using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> listOfPrefab;

    public Player GetPrefab(int index)
    {
        float x = Random.Range(-20f, 20f);
        float z = Random.Range(-20f, 20f);
        Vector3 newPos = new Vector3(x, 2, z);
        GameObject newGO = Instantiate(listOfPrefab[index], newPos, Quaternion.identity);
        Player newPlayer = newGO.GetComponent<Player>();
        newPlayer.Id = Random.Range(-100, 100);
        return newPlayer;
    }
}
