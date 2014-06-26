﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PlayerPresence : MonoBehaviour
{
    public Server Server { get; set; }
    public bool HasAuthority { get { return networkView.isMine; } }

    public static IEnumerable<PlayerPresence> AllPlayerPresences { get { return UnsafeAllPlayerPresences.ToList(); }}
    public static List<PlayerPresence> UnsafeAllPlayerPresences = new List<PlayerPresence>();

    public delegate void PlayerPresenceExistenceHandler(PlayerPresence newPlayerPresence);
    public static event PlayerPresenceExistenceHandler OnPlayerPresenceAdded = delegate {};
    public static event PlayerPresenceExistenceHandler OnPlayerPresenceRemoved = delegate {};

    public PlayerScript DefaultPlayerCharacterPrefab;

    public NetworkViewID PossessedCharacterViewID;

    public PlayerScript Possession { get; set; }

    public delegate void PlayerPresenceWantsRespawnHandler();
    public event PlayerPresenceWantsRespawnHandler OnPlayerPresenceWantsRespawn = delegate {};

    public void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
    {
        NetworkViewID prevPossesedID = PossessedCharacterViewID;
        stream.Serialize(ref PossessedCharacterViewID);
        if (stream.isReading)
        {
            if (prevPossesedID != PossessedCharacterViewID)
            {
                if (prevPossesedID != NetworkViewID.unassigned)
                {
                    NetworkView view = null;
                    try
                    {
                        view = NetworkView.Find(prevPossesedID);
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                    }
                    if (view != null) Destroy(view.gameObject);
                }
                if (PossessedCharacterViewID != NetworkViewID.unassigned)
                    PerformSpawnForViewID(PossessedCharacterViewID);
            }
        }
    }

    public void Awake()
    {
        DontDestroyOnLoad(this);
        PossessedCharacterViewID = NetworkViewID.unassigned;
    }

    public void Start()
    {
        UnsafeAllPlayerPresences.Add(this);
        OnPlayerPresenceAdded(this);
    }

    public void Update()
    {
        if (networkView.isMine)
        {
            if (Possession == null)
            {
                if (Input.GetButtonDown("Fire"))
                {
                    IndicateRespawn();
                }
            }
        }
    }

    private void IndicateRespawn()
    {
        if (Network.isServer)
            OnPlayerPresenceWantsRespawn();
        else
            networkView.RPC("ServerIndicateRespawn", RPCMode.Server);
    }

    [RPC]
    private void ServerIndicateRespawn(NetworkMessageInfo info)
    {
        if (info.sender == networkView.owner)
            OnPlayerPresenceWantsRespawn();
    }

    public void OnDestroy()
    {
        OnPlayerPresenceRemoved(this);
        UnsafeAllPlayerPresences.Remove(this);
        if (Possession != null)
        {
            Destroy(Possession.gameObject);
        }
    }

    public void SpawnCharacter(Vector3 position)
    {
        if (networkView.isMine)
            ClientSpawnCharacter(position);
        else
            networkView.RPC("ClientSpawnCharacter", networkView.owner, position);
    }

    private PlayerScript PerformSpawnForViewID(NetworkViewID characterViewId)
    {
        var newPlayerCharacter =
            (PlayerScript) Instantiate(DefaultPlayerCharacterPrefab, Vector3.zero, Quaternion.identity);
        newPlayerCharacter.networkView.viewID = characterViewId;
        newPlayerCharacter.Possessor = this;
        Possession = newPlayerCharacter;
        newPlayerCharacter.OnDeath += ReceivePawnDeath;
        return newPlayerCharacter;
    }

    [RPC]
    private void ClientSpawnCharacter(Vector3 spawnPosition)
    {
        var newViewID = Network.AllocateViewID();
        //Debug.Log("Will spawn character with view ID: " + newViewID);
        var newCharacter = PerformSpawnForViewID(newViewID);
        newCharacter.transform.position = spawnPosition;
        newCharacter.Possessor = this;
        Possession = newCharacter;
        newCharacter.OnDeath += ReceivePawnDeath;
        PossessedCharacterViewID = newViewID;
    }

    private void ReceivePawnDeath()
    {
        if (Possession != null)
        {
            Possession.OnDeath -= ReceivePawnDeath;
            Possession = null;
            PossessedCharacterViewID = NetworkViewID.unassigned;
        }
    }

    public void OnGUI()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(PlayerScript.UnsafeAllEnabledPlayerScripts.Count + " PlayerScripts");
        sb.AppendLine(UnsafeAllPlayerPresences.Count + " PlayerPresences");
        GUI.Label(new Rect(10, 10, 500, 500), sb.ToString());
    }
}