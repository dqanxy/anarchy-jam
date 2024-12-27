using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using System;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        public JumpState jumpState = JumpState.Grounded;
        private bool stopJump;
        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        bool jump;
        Vector2 move;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public Bounds Bounds => collider2d.bounds;

        //for dashing, Alexander, 12/24
        public AudioClip dashAudio;
        public float dashSpeed;
        public float dashTime;
        Vector2 dashDirection;
        float dashTimeLeft;
        //0 is can dash, 1 is dashing, 2 and 3 are can't dash
        int dashState;

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            dashState = 0;
        }

        protected override void Update()
        {
            //Alexander, 12/24
            if(1 == dashState)
            {
                dashTimeLeft -= Time.deltaTime;

                if(0 >= dashTimeLeft)
                {
                    EndDash(false);
                }
            }
            
            if (controlEnabled)
            {
                //Alexander, 12/24
                if (Input.GetKeyDown(KeyCode.X) && 0 == dashState)
                {
                    dashDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

                    if(Vector2.zero == dashDirection)
                    {
                        if (spriteRenderer.flipX)
                        {
                            dashDirection = new Vector2(-1, 0);
                        }
                        else
                        {
                            dashDirection = new Vector2(1, 0);
                        }
                    }

                    dashDirection.Normalize();
                    body.gravityScale = 0;
                    dashTimeLeft = dashTime;
                    dashState = 1;
                    audioSource.clip = dashAudio;
                    audioSource.Play();
                    //Debug.Log("attempted to play audio");
                }
                else
                {
                    if(1 != dashState)
                    {
                        move.x = Input.GetAxis("Horizontal");
                    }

                    if (jumpState == JumpState.Grounded && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.C)))
                        jumpState = JumpState.PrepareToJump;
                    else if (Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.C))
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }
                }
            }
            else
            {
                move.x = 0;
            }

            UpdateJumpState();
            base.Update();
        }

        //Alexander, 12/26
        protected override void FixedUpdate()
        {
            if (3 == dashState && IsGrounded)
            {
                spriteRenderer.color = Color.white;
                dashState = 0;
                //Debug.Log("dash state: " + dashState);
            }

            if (2 == dashState && IsGrounded)
            {
                dashState = 3;
            }

            base.FixedUpdate();
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;

                //Alexander, 12/26
                if (1 == dashState)
                {
                    EndDash(true);
                }

                jump = false;
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (move.x > 0.01f)
                spriteRenderer.flipX = false;
            else if (move.x < -0.01f)
                spriteRenderer.flipX = true;

            //Alexander, 12/25
            float speedFraction = Mathf.Abs(velocity.x) / maxSpeed;

            if(1f / 24f > speedFraction)
            {
                speedFraction = 0f;
            }

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", speedFraction);

            //Alexander, 12/24
            if (1 == dashState)
            {
                targetVelocity = dashSpeed * dashDirection;
                useFriction = false;
                return;
            }

            useFriction = 0 == move.x;

            if(0 < move.x * velocity.x)
            {
                targetVelocity = move * Math.Max(maxSpeed, Math.Abs(velocity.x));
            }
            else
            {
                targetVelocity = move * maxSpeed;
            }
            //Debug.Log(move + " " + maxSpeed + " " + targetVelocity);
        }

        //Alexander, 12/24
        //set jumped to true if the dash was ended by a jump
        public void EndDash(bool jumped)
        {
            if(1 != dashState)
            {
                return;
            }
            //Debug.Log("ending dash, " + jumped);
            if (0 <= dashDirection.y && !jumped)
            {
                //Debug.Log("resetting velocity");
                resetVelocity = true;
            }
            /*else
            {
                Debug.Log("not resetting velocity");
            }*/

            dashState = 2;
            //Debug.Log("dash state: " + dashState);
            dashDirection = new Vector2(0, 0);
            body.gravityScale = 1;
            spriteRenderer.color = new Color(118f / 255f, 236f / 255f, 1f, 1f);
        }

        //dashing into the ground should instantly interrupt the dash, but for some reason the game works more as intended when I comment out that line.
        public override void Collision()
        {
            //EndDash(false);
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}