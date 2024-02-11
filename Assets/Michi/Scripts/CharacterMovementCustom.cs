using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using System.IO;
using System;

namespace MoreMountains.TopDownEngine
{

    [AddComponentMenu("TopDown Engine/Character/Abilities/Character Movement Custom")]
    public class CharacterMovementCustom : CharacterAbility
    {

        public enum Movements { Free, Strict2DirectionsHorizontal, Strict2DirectionsVertical, Strict4Directions, Strict8Directions }


        public float MovementSpeed { get; set; }
     
        public bool MovementForbidden { get; set; }

        [Header("Direction")]

    

        public Movements Movement = Movements.Free;

        [Header("Settings")]

        public bool InputAuthorized = true;
        public bool AnalogInput = false;
        public bool ScriptDrivenInput = false;

        [Header("Speed")]

        public float WalkSpeed = 6f;
        public bool ShouldSetMovement = true;
        public float IdleThreshold = 0.05f;

        [Header("Acceleration")]

        public float Acceleration = 10f;
        public float Deceleration = 10f;
        public bool InterpolateMovementSpeed = false;
        public float MovementSpeedMaxMultiplier { get; set; } = float.MaxValue;
        private float _movementSpeedMultiplier;
        public float MovementSpeedMultiplier
        {
            get => Mathf.Min(_movementSpeedMultiplier, MovementSpeedMaxMultiplier);
            set => _movementSpeedMultiplier = value;
        }
        public Stack<float> ContextSpeedStack = new Stack<float>();
        public float ContextSpeedMultiplier => ContextSpeedStack.Count > 0 ? ContextSpeedStack.Peek() : 1;

        [Header("Walk Feedback")]
        public ParticleSystem[] WalkParticles;

        [Header("Touch The Ground Feedback")]

        public ParticleSystem[] TouchTheGroundParticles;
        public AudioClip[] TouchTheGroundSfx;

        protected float _movementSpeed;
        protected float _horizontalMovement;
        protected float _verticalMovement;
        protected Vector3 _movementVector;
        protected Vector2 _currentInput = Vector2.zero;
        protected Vector2 _normalizedInput;
        protected Vector2 _lerpedInput = Vector2.zero;
        protected float _acceleration = 0f;
        protected bool _walkParticlesPlaying = false;

        protected const string _speedAnimationParameterName = "Speed";
        protected const string _walkingAnimationParameterName = "Walking";
        protected const string _idleAnimationParameterName = "Idle";
        protected int _speedAnimationParameter;
        protected int _walkingAnimationParameter;
        protected int _idleAnimationParameter;


        public GameObject[] FootprintPrefabs; 
        public float FootprintOffsetRange = 0.5f; 
        private int _stepCount = 0; 
        public int StepsPerFootprint = 20;  
        private string playerID;
        public string timestamp;



        [System.Serializable]
        public class FootprintDataList
        {
            public List<FootprintData> footprints = new List<FootprintData>();
        }

        [System.Serializable]
        public class FootprintData
        {
            public Vector3 position;
            public Quaternion rotation;
            public int prefabIndex;
            public string playerID;
            public string timestamp;
            public float colorR; 
            public float colorG;
            public float colorB; 
            public float colorA = 1;
        }


        public FootprintDataList allFootprints = new FootprintDataList();


        protected void CreateFootprint()
        {
            if (FootprintPrefabs != null && FootprintPrefabs.Length > 0)
            {
                
                GameObject selectedFootprintPrefab = FootprintPrefabs[UnityEngine.Random.Range(0, FootprintPrefabs.Length)];
                Vector3 footprintPosition = transform.position + new Vector3(UnityEngine.Random.Range(-FootprintOffsetRange, FootprintOffsetRange), 0, UnityEngine.Random.Range(-FootprintOffsetRange, FootprintOffsetRange)); 
                Vector2 movementDirection = new Vector2(_horizontalMovement, _verticalMovement);
                float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg;
                Quaternion footprintRotation = Quaternion.Euler(0f, 0f, angle - 90f); 

                Instantiate(selectedFootprintPrefab, footprintPosition, footprintRotation);

                FootprintData data = new FootprintData();
                data.position = footprintPosition;
                data.rotation = footprintRotation;
                data.prefabIndex = UnityEngine.Random.Range(0, FootprintPrefabs.Length);
                data.playerID = this.playerID;
                data.timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
                data.colorR = playerColor.r;
                data.colorG = playerColor.g;
                data.colorB = playerColor.b;
                data.colorA = playerColor.a;
                allFootprints.footprints.Add(data);
            }
        }

        protected override void Initialization()
        {
            base.Initialization();
            GeneratePlayerID();
            LoadFootprints();
            ResetAbility();
        }

        public override void ResetAbility()
        {
            base.ResetAbility();
            MovementSpeed = WalkSpeed;
            if (ContextSpeedStack != null)
            {
                ContextSpeedStack.Clear();
            }
            if ((_movement != null) && (_movement.CurrentState != CharacterStates.MovementStates.FallingDownHole))
            {
                _movement.ChangeState(CharacterStates.MovementStates.Idle);
            }
            MovementSpeedMultiplier = 1f;
            MovementForbidden = false;

            foreach (ParticleSystem system in TouchTheGroundParticles)
            {
                if (system != null)
                {
                    system.Stop();
                }
            }
            foreach (ParticleSystem system in WalkParticles)
            {
                if (system != null)
                {
                    system.Stop();
                }
            }
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();

            HandleFrozen();

            if (!AbilityAuthorized
                || ((_condition.CurrentState != CharacterStates.CharacterConditions.Normal) && (_condition.CurrentState != CharacterStates.CharacterConditions.ControlledMovement)))
            {
                if (AbilityAuthorized)
                {
                    StopAbilityUsedSfx();
                }
                return;
            }
            HandleDirection();
            HandleMovement();
            Feedbacks();
        }

        protected override void HandleInput()
        {
            if (ScriptDrivenInput)
            {
                return;
            }

            if (InputAuthorized)
            {
                _horizontalMovement = _horizontalInput;
                _verticalMovement = _verticalInput;
            }
            else
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
            }
        }

        /// <param name="value">Horizontal move value, between -1 and 1 - positive : will move to the right, negative : will move left </param>
        public virtual void SetMovement(Vector2 value)
        {
            _horizontalMovement = value.x;
            _verticalMovement = value.y;
        }

        /// <param name="value"></param>
        public virtual void SetHorizontalMovement(float value)
        {
            _horizontalMovement = value;
        }

        /// <param name="value"></param>
        public virtual void SetVerticalMovement(float value)
        {
            _verticalMovement = value;
        }

        /// <param name="movementMultiplier"></param>
        /// <param name="duration"></param>
        public virtual void ApplyMovementMultiplier(float movementMultiplier, float duration)
        {
            StartCoroutine(ApplyMovementMultiplierCo(movementMultiplier, duration));
        }

        /// <param name="movementMultiplier"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        protected virtual IEnumerator ApplyMovementMultiplierCo(float movementMultiplier, float duration)
        {
            if (_characterMovement == null)
            {
                yield break;
            }
            SetContextSpeedMultiplier(movementMultiplier);
            yield return MMCoroutine.WaitFor(duration);
            ResetContextSpeedMultiplier();
        }

        /// <param name="newMovementSpeedMultiplier"></param>
        public virtual void SetContextSpeedMultiplier(float newMovementSpeedMultiplier)
        {
            ContextSpeedStack.Push(newMovementSpeedMultiplier);
        }

        public virtual void ResetContextSpeedMultiplier()
        {
            if (ContextSpeedStack.Count <= 0)
            {
                return;
            }

            ContextSpeedStack.Pop();
        }

        protected virtual void HandleDirection()
        {
            switch (Movement)
            {
                case Movements.Free:
                    // do nothing
                    break;
                case Movements.Strict2DirectionsHorizontal:
                    _verticalMovement = 0f;
                    break;
                case Movements.Strict2DirectionsVertical:
                    _horizontalMovement = 0f;
                    break;
                case Movements.Strict4Directions:
                    if (Mathf.Abs(_horizontalMovement) > Mathf.Abs(_verticalMovement))
                    {
                        _verticalMovement = 0f;
                    }
                    else
                    {
                        _horizontalMovement = 0f;
                    }
                    break;
                case Movements.Strict8Directions:
                    _verticalMovement = Mathf.Round(_verticalMovement);
                    _horizontalMovement = Mathf.Round(_horizontalMovement);
                    break;
            }
        }

        protected virtual void HandleMovement()
        {
            if ((_movement.CurrentState != CharacterStates.MovementStates.Walking) && _startFeedbackIsPlaying)
            {
                StopStartFeedbacks();
            }

            if (_movement.CurrentState != CharacterStates.MovementStates.Walking && _abilityInProgressSfx != null)
            {
                StopAbilityUsedSfx();
            }

            if (_movement.CurrentState == CharacterStates.MovementStates.Walking && _abilityInProgressSfx == null)
            {
                PlayAbilityUsedSfx();
            }

            if (!AbilityAuthorized
                 || (_condition.CurrentState != CharacterStates.CharacterConditions.Normal))
            {
                return;
            }

            CheckJustGotGrounded();

            if (MovementForbidden)
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
            }

            if (!_controller.Grounded
                && (_condition.CurrentState == CharacterStates.CharacterConditions.Normal)
                && (
                    (_movement.CurrentState == CharacterStates.MovementStates.Walking)
                    || (_movement.CurrentState == CharacterStates.MovementStates.Idle)
                ))
            {
                _movement.ChangeState(CharacterStates.MovementStates.Falling);
            }

            if (_controller.Grounded && (_movement.CurrentState == CharacterStates.MovementStates.Falling))
            {
                _movement.ChangeState(CharacterStates.MovementStates.Idle);
            }

            if (_controller.Grounded
                 && (_controller.CurrentMovement.magnitude > IdleThreshold)
                 && (_movement.CurrentState == CharacterStates.MovementStates.Idle))
            {
                _movement.ChangeState(CharacterStates.MovementStates.Walking);
                PlayAbilityStartSfx();
                PlayAbilityUsedSfx();
                PlayAbilityStartFeedbacks();
            }

            if ((_movement.CurrentState == CharacterStates.MovementStates.Walking)
                && (_controller.CurrentMovement.magnitude <= IdleThreshold))
            {
                _movement.ChangeState(CharacterStates.MovementStates.Idle);
                PlayAbilityStopSfx();
                PlayAbilityStopFeedbacks();
            }


            if (_controller.CurrentMovement.magnitude > IdleThreshold)
            {
                _stepCount++;
                if (_stepCount >= StepsPerFootprint)
                {
                    CreateFootprint();
                    _stepCount = 0;
                }
            }
            else
            {
                _stepCount = 0;
            }


            if (ShouldSetMovement)
            {
                SetMovement();
            }
        }


//        public void SaveFootprints()
//        {
//            string jsonData = JsonUtility.ToJson(allFootprints, true);
//            File.WriteAllText(GetFilePath(), jsonData);
//        }







        public void SaveFootprints()
        {
            // Laden der bestehenden Daten, um sicherzustellen, dass wir diese nicht überschreiben
            FootprintDataList existingData = new FootprintDataList();
            if (File.Exists(GetFilePath()))
            {
                string existingJson = File.ReadAllText(GetFilePath());
                existingData = JsonUtility.FromJson<FootprintDataList>(existingJson);
            }

            // Hinzufügen der Fußspuren des aktuellen Spielers zu den bestehenden Daten
            foreach (var footprint in allFootprints.footprints)
            {
                if (footprint.playerID == this.playerID) // Überprüfen, ob die Fußspur zum aktuellen Spieler gehört
                {
                    existingData.footprints.Add(footprint);
                }
            }

            // Speichern der aktualisierten Liste von Fußspuren
            string jsonData = JsonUtility.ToJson(existingData, true);
            File.WriteAllText(GetFilePath(), jsonData);
        }












        public void LoadFootprints()
        {
            int highestID = 0;
            float desiredAlpha = 0.5f; // Setzen Sie hier den gewünschten Alpha-Wert

            if (File.Exists(GetFilePath()))
            {
                string jsonData = File.ReadAllText(GetFilePath());
                FootprintDataList existingData = JsonUtility.FromJson<FootprintDataList>(jsonData);
                allFootprints.footprints.AddRange(existingData.footprints);

                foreach (var footprint in existingData.footprints)
                {
                    GameObject selectedFootprintPrefab = FootprintPrefabs[footprint.prefabIndex];
                    GameObject footprintInstance = Instantiate(selectedFootprintPrefab, footprint.position, footprint.rotation);

                    // Erhalten des SpriteRenderer und Anpassen des Alpha-Werts
                    SpriteRenderer spriteRenderer = footprintInstance.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        Color currentColor = spriteRenderer.color;
                        currentColor.a = desiredAlpha; // Anpassen des Alpha-Werts
                        spriteRenderer.color = currentColor;
                    }

                    int id;
                    bool success = int.TryParse(footprint.playerID, out id);
                    if (success && id > highestID)
                    {
                        highestID = id;
                    }
                }

                playerID = (highestID + 1).ToString("D3");
            }
            else
            {
                playerID = "001";
            }
        }



















        private string GetFilePath()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string folderName = "TopDownData";

            string folderPath = Path.Combine(desktopPath, folderName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return Path.Combine(folderPath, "footprints.json");
        }


        private Color playerColor;

        private void GeneratePlayerID()
        {
            playerID = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            playerColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        }

        protected virtual void HandleFrozen()
        {
            if (!AbilityAuthorized)
            {
                return;
            }
            if (_condition.CurrentState == CharacterStates.CharacterConditions.Frozen)
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
                SetMovement();
            }
        }

        protected virtual void SetMovement()
        {
            _movementVector = Vector3.zero;
            _currentInput = Vector2.zero;

            _currentInput.x = _horizontalMovement;
            _currentInput.y = _verticalMovement;

            _normalizedInput = _currentInput.normalized;

            float interpolationSpeed = 1f;

            if ((Acceleration == 0) || (Deceleration == 0))
            {
                _lerpedInput = AnalogInput ? _currentInput : _normalizedInput;
            }
            else
            {
                if (_normalizedInput.magnitude == 0)
                {
                    _acceleration = Mathf.Lerp(_acceleration, 0f, Deceleration * Time.deltaTime);
                    _lerpedInput = Vector2.Lerp(_lerpedInput, _lerpedInput * _acceleration, Time.deltaTime * Deceleration);
                    interpolationSpeed = Deceleration;
                }
                else
                {
                    _acceleration = Mathf.Lerp(_acceleration, 1f, Acceleration * Time.deltaTime);
                    _lerpedInput = AnalogInput ? Vector2.ClampMagnitude(_currentInput, _acceleration) : Vector2.ClampMagnitude(_normalizedInput, _acceleration);
                    interpolationSpeed = Acceleration;
                }
            }

            _movementVector.x = _lerpedInput.x;
            _movementVector.y = 0f;
            _movementVector.z = _lerpedInput.y;

            if (InterpolateMovementSpeed)
            {
                _movementSpeed = Mathf.Lerp(_movementSpeed, MovementSpeed * ContextSpeedMultiplier * MovementSpeedMultiplier, interpolationSpeed * Time.deltaTime);
            }
            else
            {
                _movementSpeed = MovementSpeed * MovementSpeedMultiplier * ContextSpeedMultiplier;
            }

            _movementVector *= _movementSpeed;

            if (_movementVector.magnitude > MovementSpeed * ContextSpeedMultiplier * MovementSpeedMultiplier)
            {
                _movementVector = Vector3.ClampMagnitude(_movementVector, MovementSpeed);
            }

            if ((_currentInput.magnitude <= IdleThreshold) && (_controller.CurrentMovement.magnitude < IdleThreshold))
            {
                _movementVector = Vector3.zero;
            }

            _controller.SetMovement(_movementVector);
        }

        protected virtual void CheckJustGotGrounded()
        {
            if (_controller.JustGotGrounded)
            {
                _movement.ChangeState(CharacterStates.MovementStates.Idle);
            }
        }

        protected virtual void Feedbacks()
        {
            if (_controller.Grounded)
            {
                if (_controller.CurrentMovement.magnitude > IdleThreshold)
                {
                    foreach (ParticleSystem system in WalkParticles)
                    {
                        if (!_walkParticlesPlaying && (system != null))
                        {
                            system.Play();
                        }
                        _walkParticlesPlaying = true;
                    }
                }
                else
                {
                    foreach (ParticleSystem system in WalkParticles)
                    {
                        if (_walkParticlesPlaying && (system != null))
                        {
                            system.Stop();
                            _walkParticlesPlaying = false;
                        }
                    }
                }
            }
            else
            {
                foreach (ParticleSystem system in WalkParticles)
                {
                    if (_walkParticlesPlaying && (system != null))
                    {
                        system.Stop();
                        _walkParticlesPlaying = false;
                    }
                }
            }

            if (_controller.JustGotGrounded)
            {
                foreach (ParticleSystem system in TouchTheGroundParticles)
                {
                    if (system != null)
                    {
                        system.Clear();
                        system.Play();
                    }
                }
                foreach (AudioClip clip in TouchTheGroundSfx)
                {
                    MMSoundManagerSoundPlayEvent.Trigger(clip, MMSoundManager.MMSoundManagerTracks.Sfx, this.transform.position);
                }
            }
        }

        public virtual void ResetSpeed()
        {
            MovementSpeed = WalkSpeed;
        }

        protected override void OnRespawn()
        {
            ResetSpeed();
            MovementForbidden = false;
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            DisableWalkParticles();
        }

        protected virtual void DisableWalkParticles()
        {
            if (WalkParticles.Length > 0)
            {
                foreach (ParticleSystem walkParticle in WalkParticles)
                {
                    if (walkParticle != null)
                    {
                        walkParticle.Stop();
                    }
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SaveFootprints();
            DisableWalkParticles();
            PlayAbilityStopSfx();
            PlayAbilityStopFeedbacks();
            StopAbilityUsedSfx();
        }

        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_speedAnimationParameterName, AnimatorControllerParameterType.Float, out _speedAnimationParameter);
            RegisterAnimatorParameter(_walkingAnimationParameterName, AnimatorControllerParameterType.Bool, out _walkingAnimationParameter);
            RegisterAnimatorParameter(_idleAnimationParameterName, AnimatorControllerParameterType.Bool, out _idleAnimationParameter);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _speedAnimationParameter, Mathf.Abs(_controller.CurrentMovement.magnitude), _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _walkingAnimationParameter, (_movement.CurrentState == CharacterStates.MovementStates.Walking), _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _idleAnimationParameter, (_movement.CurrentState == CharacterStates.MovementStates.Idle), _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}