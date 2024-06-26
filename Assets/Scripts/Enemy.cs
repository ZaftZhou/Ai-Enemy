using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using BasicAI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : BaseStateMachine {
    [SerializeField] Transform lookFrom; // head of enemy skeleton
    [SerializeField] Transform lookTo; // chest of player skeleton
    [SerializeField] GameObject playerObj;

    [SerializeField] Transform camTransform; // to orient the symbol toward the camera
    [SerializeField] TMP_Text symbol;
    // TODO make a field for the Animator to pass to AI states

    public float waryWaitTime = 1; // how long to pause in the wary state
    public float visionWaitTime = .5f; // how long between vision updates
    public float visionRadius = .25f; // width of the visibility spherecast
    public float visionDistance = 50; // how far away the player is visible
    public float detectDistance = 15; // how far away to detect player regardless of visibility
    public float patrolSpeed = 4;
    public float chaseSpeed = 8;

    [Range(0.1f, 0.9f)]
    public float facingDotThreshold = .35f; // lower is a wider field of vision

    public bool CanSeePlayer { get; private set; }
    public bool IsDetectingPlayer { get; private set; } // sensing other than vision
    public Vector3 LastPlayerPosition { get; private set; }

    private Coroutine visionLoop;

    public NavMeshAgent Agent { get; private set; }

    protected override void Start() {
        Agent = GetComponent<NavMeshAgent>();

        currentState = new WaryState(this, waryWaitTime, symbol);

        availableStates = new Dictionary<Type, BaseState>() {
            { typeof(WaryState), currentState},
            { typeof(PatrolState), new PatrolState(this, moveTargetThreshold, patrolSpeed)},
            { typeof(ChaseState), new ChaseState(this, moveTargetThreshold, chaseSpeed)}
        };

        // vision code is a coroutine instead of running from Update
        visionLoop = StartCoroutine(VisionCoroutine());
    }

    protected override void Update() {
        symbol.transform.rotation = Quaternion.Euler(0, camTransform.eulerAngles.y, 0);

        base.Update();
    }

    private IEnumerator VisionCoroutine() {

        var offset = UnityEngine.Random.value * visionWaitTime;
        yield return new WaitForSeconds(offset);

        while (lookFrom && playerObj != null) {
            CanSeePlayer = false;
            IsDetectingPlayer = false;
            var doWaitTime = currentState.GetType() == typeof(PatrolState);

            var playerOffset = lookTo.position - lookFrom.position;

            // first check if the player is close enough to see
            var dist = playerOffset.magnitude;
            if (dist < visionDistance) {

                // then use the dot product to see if the player is within the field of view
                var dot = Vector3.Dot(lookFrom.forward, playerOffset.normalized);
                var doRaycast = dot >= facingDotThreshold;

                // only then do a raycast for line of sight
                if (doRaycast) {
                    if (Physics.SphereCast(lookFrom.position, visionRadius, playerOffset, out var hit)) {
                        if (hit.transform.gameObject == playerObj) {
                            CanSeePlayer = true;
                            LastPlayerPosition = playerObj.transform.position;
                        }
                    }

                    if (showRuntimeDebug) {
                        var tint = CanSeePlayer ? Color.blue : Color.yellow;
                        if (doWaitTime) {
                            Debug.DrawRay(lookFrom.position, playerOffset, tint, visionWaitTime);
                        } else {
                            Debug.DrawRay(lookFrom.position, playerOffset, tint);
                        }
                    }
                }

                // detect very close players regardless of visibility (eg. can hear)
                if (dist < detectDistance) {
                    IsDetectingPlayer = true;
                    LastPlayerPosition = playerObj.transform.position;
                }
            }

            // while patrolling: pause before checking again, to simulate reaction time
            if (doWaitTime) {
                yield return new WaitForSeconds(visionWaitTime);
            } else {
                yield return null;
            }
        }
    }
}
