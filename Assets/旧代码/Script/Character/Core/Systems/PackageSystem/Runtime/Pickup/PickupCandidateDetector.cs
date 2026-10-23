using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects nearby WorldPickupItems via trigger overlaps.
/// Maintains a sorted list of candidates by distance.
/// Must be on the same GameObject as a trigger Collider2D.
/// </summary>
public class PickupCandidateDetector : MonoBehaviour
{
    private readonly List<WorldPickupItem> _candidates = new List<WorldPickupItem>();
    private WorldPickupItem _currentBest;
    private float _switchCooldown = 0.15f;
    private float _lastSwitchTime;

    public WorldPickupItem CurrentBest => _currentBest;
    public int CandidateCount => _candidates.Count;
    public bool HasCandidate => _currentBest != null;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null && !_candidates.Contains(pickup))
        {
            _candidates.Add(pickup);
            UpdateBestCandidate(force: true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var pickup = other.GetComponent<WorldPickupItem>();
        if (pickup != null)
        {
            _candidates.Remove(pickup);
            if (_currentBest == pickup)
            {
                _currentBest = null;
                UpdateBestCandidate(force: true);
            }
        }
    }

    private void Update()
    {
        // Clean up destroyed/missing candidates
        _candidates.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // Periodically re-evaluate best candidate (with small cooldown to prevent flicker)
        if (Time.time - _lastSwitchTime >= _switchCooldown)
        {
            UpdateBestCandidate(force: false);
        }
    }

    private void UpdateBestCandidate(bool force)
    {
        if (_candidates.Count == 0)
        {
            if (_currentBest != null)
            {
                _currentBest = null;
                _lastSwitchTime = Time.time;
            }
            return;
        }

        WorldPickupItem best = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in _candidates)
        {
            if (candidate == null) continue;
            float dist = (candidate.transform.position - transform.position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        if (best != _currentBest)
        {
            if (force || Time.time - _lastSwitchTime >= _switchCooldown)
            {
                _currentBest = best;
                _lastSwitchTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Remove a candidate from the list (e.g. after pickup).
    /// </summary>
    public void RemoveCandidate(WorldPickupItem item)
    {
        if (item == null) return;
        _candidates.Remove(item);
        if (_currentBest == item)
            _currentBest = null;
    }
}
