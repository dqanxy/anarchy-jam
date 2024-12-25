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
        public float dashSpeed;
        public float dashTime;
        Vector2 dashDirection;
        float dashTimeLeft;
        int dashState;
        //0 is can dash, 1 is dashing, 2 is can't dash

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
                    EndDash();
                }
            }
            else if (controlEnabled)
            {
                //Alexander, 12/24
                if (Input.GetKeyDown(KeyCode.X) && 0 == dashState)
                {
                    dashDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
                    dashDirection.Normalize();
                    body.gravityScale = 0;
                    dashTimeLeft = dashTime;
                    dashState = 1;
                    //Debug.Log("dash state: " + dashState);
                }
                else
                {
                    move.x = Input.GetAxis("Horizontal");
                    if (jumpState == JumpState.Grounded && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.C)))
                        jumpState = JumpState.PrepareToJump;
                    else if (Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.C))
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }

                    //Alexander, 12/24
                    if (2 == dashState && IsGrounded)
                    {
                        spriteRenderer.color = Color.white;
                        dashState = 0;
                        //Debug.Log("dash state: " + dashState);
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

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);

            //Alexander, 12/24
            if (1 == dashState)
            {
                targetVelocity = dashSpeed * dashDirection;
                useFriction = false;
                return;
            }

            useFriction = 0 == move.x;

            if(0 < move.x * body.linearVelocity.x)
            {
                targetVelocity = move * Math.Max(maxSpeed, Math.Abs(body.linearVelocity.x));
            }
            else
            {
                targetVelocity = move * maxSpeed;
            }
            //Debug.Log(move + " " + maxSpeed + " " + targetVelocity);
        }

        //Alexander, 12/24
        public void EndDash()
        {
            if(1 != dashState)
            {
                return;
            }
            
            if (0 <= dashDirection.y)
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

        public override void Collision()
        {
            EndDash();
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