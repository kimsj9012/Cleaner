using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class SpawnManager : MonoBehaviour   // 몬스터 생성, 죽음, 리스폰 및 아이템 드랍 관리
{
    public event System.Action<DropItem> ItemDropEvent;    // 충돌 체크 클래스에 드랍 아이템 전달

    GameObject spawner;
    public GameObject spot;

    public Queue<DropItem> itemPouchs = new Queue<DropItem>();  // 아이템 풀 (정보x)
    private List<DropItem> dropPouchs = new List<DropItem>();   // 땅에 떨어진 아이템 리스트 (드랍 순서대로 습득하지 않을 수 있기 때문)

    public List<int> areas = new List<int>();
    public List<SpawnSpot> spots = new List<SpawnSpot>();
    private List<Dictionary<string, object>> spawnData;

    private const int maxMonsterType = 3;
    private const int maxQueueSize = 8;

    private void Awake()
    {
        spawner = new GameObject("Spawner");
        spawner.tag = "Spawner";
    }
    private void Start()
    {
        spawnData = DataManager.Instance.monsterSpawnData;
        ResourceManager.Instance.LoadObj("Monsters");
        ResourceManager.Instance.LoadObj("Items", "ItemPouch");

        AddItem();
        AddAreaInScene();
        AddSpotInScene();
        AddMonster();

        for (int i = 0; i < spots.Count; i++)
        {
            if(spots[i].monsters.Count != 0)
            {
                int ran = Random.Range(0, spots[i].monsters.Count);
                GameObject sceneMonster = spots[i].monsters[ran];
                sceneMonster.SetActive(true);
            }
        }

        MonsterDeath.ExitAnimation += Death; 
    }

    void OnDisable()
    {
        MonsterDeath.ExitAnimation -= Death;
    }

    void Death(GameObject monster)
    {
        StartCoroutine(MonsterDie(monster));
        StartCoroutine(Respawner(monster));
    }

    IEnumerator MonsterDie(GameObject monster)  // 몬스터 사라짐 지연
    {
        DequeueItem(monster);
        float time = 10f;
        while(time > 0f)
        {
            time -= Time.deltaTime;
            yield return null;
        }
        monster.SetActive(false);
    }
    IEnumerator Respawner(GameObject monster)   // 몬스터 리스폰 함수. 
    {
        float time = monster.GetComponent<IMonsterStatusInterface>().RespawnTime;
        while (time > 0f)
        {
            time -= Time.deltaTime;
            yield return null;
        }
        SpawnSpot spot = monster.transform.parent.GetComponent<SpawnSpot>();
        int ran = Random.Range(0, spot.monsters.Count);

        spot.monsters[ran].transform.localPosition = Vector3.zero;
        spot.monsters[ran].SetActive(true);
    }

    void AddAreaInScene()   // 씬에 구역 추가
    {
        for (int i = 0; i < DataManager.Instance.mapData.Count; i++)
        {
            if (DataManager.Instance.mapData[i]["Scene"].ToString() == SceneManager.GetActiveScene().name)
            {
                areas.Add((int)DataManager.Instance.mapData[i]["ID"]);
            }
        }
    }

    void AddSpotInScene()   // 씬의 모든 스폰 스팟(구역 아이디 포함)
    {
        for (int i = 0; i < spawnData.Count; i++)
        {
            int mapID = (int)spawnData[i]["Map_ID"];
            if (areas.Contains(mapID))
            {
                float x = float.Parse(spawnData[i]["X"].ToString());
                float y = float.Parse(spawnData[i]["Y"].ToString());
                float z = float.Parse(spawnData[i]["Z"].ToString());
                float rot_Y = float.Parse(spawnData[i]["Rotation_Y"].ToString());

                Vector3 pos = new Vector3(x, y, z);
                Quaternion rot = Quaternion.AngleAxis(rot_Y, Vector3.up);
                GameObject spotClone = Instantiate(spot, pos,rot,spawner.transform);

                spotClone.name = "spot" + i;
                SpawnSpot spawn = spotClone.GetComponent<SpawnSpot>();
                spawn.id = (int)spawnData[i]["ID"];
                spots.Add(spawn);
            }
        }
    }

    void AddMonster()   // 스팟의 몬스터 정보대로 몬스터 생성
    {
        for(int i = 0; i < spots.Count; i++)  // 스팟 개수만큼 
        {
            SpawnSpot spot = spots[i].GetComponent<SpawnSpot>();
            for (int index = 0; index < maxMonsterType; index++)    // 스팟 별 생성 가능 몬스터 종류 만큼 (최대 3)
            {
                int.TryParse(spawnData[spots[i].id-1]["Monster_ID_"+(index+1)].ToString(), out int id);   // 몬스터 아이디
                if (DataManager.Instance.monsterMap.TryGetValue(id, out MonsterInfo value)) // 몬스터 딕셔너리에서 몬스터 정보 꺼내오기
                {
                    GameObject monster = Instantiate(ResourceManager.Instance.GetObj(value.monsterName),spots[i].transform);   // 몬스터 정보 (이름)으로 리소스 검색 후 생성
                    IMonsterStatusInterface i_status = monster.GetComponent<IMonsterStatusInterface>();     // 몬스터 정보 인터페이스

                    i_status.SetInfo(value); // 리소스에 몬스터 정보 Set
                    monster.transform.localPosition = Vector3.zero;

                    spot.monsters.Add(monster);
                    monster.SetActive(false);
                }
            }
        }
    }
    void AddItem()  // 아이템 풀 생성
    {
        for(int i =0; i < maxQueueSize; i++)
        {
            GameObject itemObj = Instantiate(ResourceManager.Instance.GetObj("ItemPouch"),spawner.transform);
            itemObj.SetActive(false);
            DropItem item = itemObj.GetComponent<DropItem>();

            itemPouchs.Enqueue(item);
        }
    }
    public void EnqueueItem(DropItem item)  // 아이템 습득 시 
    {
        for(int i=0; i < dropPouchs.Count; i++)
        {
            if(item == dropPouchs[i])
            {
                itemPouchs.Enqueue(item);   
                dropPouchs.Remove(dropPouchs[i]);
                item.gameObject.SetActive(false);
            }
        }
    }
    public void DequeueItem(GameObject dropMonster) // 몬스터 처치 시 아이템 정보 Set
    {
        if(itemPouchs.Count == 0)
        {
            itemPouchs.Enqueue(dropPouchs[0]);
            dropPouchs.RemoveAt(0);
        }
        DropItem item = itemPouchs.Dequeue();
        dropPouchs.Add(item);

        item.SetItemData(dropMonster);
        item.gameObject.transform.position = dropMonster.transform.position;
        item.gameObject.SetActive(true);

        ItemDropEvent?.Invoke(item);
    }

}
