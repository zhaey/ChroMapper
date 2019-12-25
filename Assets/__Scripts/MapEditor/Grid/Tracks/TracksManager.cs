﻿using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class TracksManager : MonoBehaviour
{
    [SerializeField] private GameObject TrackPrefab;
    [SerializeField] private Transform TracksParent;
    [SerializeField] private EventsContainer events;
    [SerializeField] private List<Track> loadedTracks = new List<Track>();

    private List<BeatmapObjectContainerCollection> objectContainerCollections;

    // Start is called before the first frame update
    void Start()
    {
        objectContainerCollections = GetComponents<BeatmapObjectContainerCollection>()
            .Where(x => x is NotesContainer || x is ObstaclesContainer).ToList();
        BeatmapObjectContainer.FlaggedForDeletionEvent += FlaggedForDeletion;
    }

    private void FlaggedForDeletion(BeatmapObjectContainer obj)
    {
        //Refresh the tracks if we delete any rotation event
        if (obj is BeatmapEventContainer e && (e.eventData._type == MapEvent.EVENT_TYPE_EARLY_ROTATION || e.eventData._type == MapEvent.EVENT_TYPE_LATE_ROTATION))
            RefreshTracks();
    }

    private void OnDestroy()
    {
        BeatmapObjectContainer.FlaggedForDeletionEvent -= FlaggedForDeletion;
    }

    public void RefreshTracks()
    {
        if (!loadedTracks.Any())
        {
            for (int i = 0; i < 24; i++)
            {
                Track track = Instantiate(TrackPrefab, TracksParent).GetComponent<Track>();
                track.gameObject.name = $"Track {15 * i}";
                track.AssignRotationValue(15 * i, false);
                loadedTracks.Add(track);
            }
        }else foreach (Track track in loadedTracks)
            track.AssignRotationValue(0);
        List<BeatmapEventContainer> allRotationEvents = events.LoadedContainers.Cast<BeatmapEventContainer>().Where(x =>
            x.eventData._type == MapEvent.EVENT_TYPE_EARLY_ROTATION ||
            x.eventData._type == MapEvent.EVENT_TYPE_LATE_ROTATION).OrderBy(x => x.eventData._time).ToList();

        List<BeatmapObjectContainer> allObjects = new List<BeatmapObjectContainer>();
        objectContainerCollections.ForEach(x => allObjects.AddRange(x.LoadedContainers));

        //Filter out bad rotation events (Legacy MM BPM changes, custom platform events using Events 14 and 15, etc.)
        allRotationEvents = allRotationEvents.Where(x => x.eventData._value > 0 &&
            x.eventData._value < MapEvent.LIGHT_VALUE_TO_ROTATION_DEGREES.Count()).ToList();

        if (allRotationEvents.Count == 0)
        {
            Track track = loadedTracks.First();
            foreach (BeatmapObjectContainer obj in allObjects) track.AttachContainer(obj, 0);
            return;
        }

        int rotation = 0;
        List<BeatmapObjectContainer> firstObjects = allObjects.Where(x =>
            (x.objectData._time < allRotationEvents.First().eventData._time && allRotationEvents.First().eventData._type == MapEvent.EVENT_TYPE_EARLY_ROTATION) ||
            (x.objectData._time <= allRotationEvents.First().eventData._time && allRotationEvents.First().eventData._type == MapEvent.EVENT_TYPE_LATE_ROTATION)
        ).ToList();
        firstObjects.ForEach(x => loadedTracks.First().AttachContainer(x, rotation));
        for (int i = 0; i < allRotationEvents.Count - 1; i++)
        {
            float firstTime = allRotationEvents[i].eventData._time;
            float secondTime = allRotationEvents[i + 1].eventData._time;
            rotation += MapEvent.LIGHT_VALUE_TO_ROTATION_DEGREES[allRotationEvents[i].eventData._value];
            int localRotation = betterModulo(rotation, 360);
            List<BeatmapObjectContainer> rotatedObjects = allObjects.Where(x =>
                ((x.objectData._time >= firstTime && allRotationEvents[i].eventData._type == MapEvent.EVENT_TYPE_EARLY_ROTATION) ||
                (x.objectData._time > firstTime && allRotationEvents[i].eventData._type == MapEvent.EVENT_TYPE_LATE_ROTATION)) &&
                ((x.objectData._time < secondTime && allRotationEvents[i + 1].eventData._type == MapEvent.EVENT_TYPE_EARLY_ROTATION) ||
                (x.objectData._time <= secondTime && allRotationEvents[i + 1].eventData._type == MapEvent.EVENT_TYPE_LATE_ROTATION))
                ).ToList();
            Track track = loadedTracks.Where(x => x.RotationValue == localRotation).FirstOrDefault();
            rotatedObjects.ForEach(x => track?.AttachContainer(x, rotation));
        }
        foreach (Track track in loadedTracks)
        {
            if (Settings.Instance.RotateTrack)
                track.AssignRotationValue(track.RotationValue);
            else track.AssignRotationValue(0);
        }
    }

    private int betterModulo(int x, int m) => (x % m + m) % m; //thanks stackoverflow

    public void UpdatePosition(float position)
    {
        foreach (Track track in loadedTracks) track.UpdatePosition(position);
    }

    public Track GetTrackForRotationValue(float rotation)
    {
        int roundedRotation = Mathf.RoundToInt(rotation);
        int localRotation = betterModulo(roundedRotation, 360);
        return loadedTracks.Where(x => x.RotationValue == localRotation).FirstOrDefault();
    }
}