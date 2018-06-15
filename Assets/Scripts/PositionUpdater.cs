﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class PositionUpdater : MonoBehaviour
{
	private Animator animator;
	private const int poseCanvasWidth = 640;
	private const int poseCanvasHeight = 480;
	private const float cameraZIndex = 15.0f;
	private const float accuracyThreshold = 0.3f;

	private bool isReady = false;
	private PoseVector currentPoseVector;

	void Start ()
	{
		animator = gameObject.GetComponent<Animator>();
		animator.SetInteger("animation", 19);
	}

	void OnAnimatorIK(int layerIndex)
	{
		if (!isReady) return;

		updateCoreBodyPosition();
		updateHeadPosition();
		updateArmsPosition();
	}

	void setCanvasSize(string jsonString)
	{
//		CanvasSize canvas = JsonUtility.FromJson<CanvasSize>(jsonString);
	}

	void updatePosition(string jsonString)
	{
		Pose pose = JsonUtility.FromJson<Pose>(jsonString);
		if (isReady) {
			currentPoseVector = new PoseVector(pose);
		} else if (isInitialPoseScoreGood(pose)) {
			isReady = true;
			currentPoseVector = new PoseVector(pose);
		}
	}

	private bool isInitialPoseScoreGood(Pose pose)
	{
		bool isGoodScore = true;
		string[] coreBodyKeypoints = {
			"nose", "leftShoulder", "rightShoulder", "leftElbow", "rightElbow", "leftWrist", "rightWrist"
		};
		foreach (FieldInfo field in pose.GetType().GetFields())
		{
			if (!(coreBodyKeypoints.Contains(field.Name))) continue;

			Keypoint keypoint = (Keypoint)field.GetValue(pose);
			if (keypoint.score < 0.5f) {
				isGoodScore = false;
				break;
			}
		};
		return isGoodScore;
	}

	private void updateCoreBodyPosition()
	{
		Vector3 rightShoulderPosition = currentPoseVector.rightShoulder.position;
		rightShoulderPosition.z = cameraZIndex;
		Vector3 worldRightShoulderPosition = Camera.main.ScreenToWorldPoint(rightShoulderPosition);

		Vector3 leftShoulderPosition = currentPoseVector.leftShoulder.position;
		leftShoulderPosition.z = cameraZIndex;
		Vector3 worldLeftShoulderPosition = Camera.main.ScreenToWorldPoint(leftShoulderPosition);

		Vector3 position = transform.position;
		position.x = (worldRightShoulderPosition.x + worldLeftShoulderPosition.x) / 2 ;
		transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 2);
	}

	private void updateHeadPosition()
	{
		if (!isEyesAndNoseScoreGood(currentPoseVector)) return;
		if (!isLeftOrRightEarScoreGood(currentPoseVector)) return;

		float leftLength = (currentPoseVector.leftEar.position - currentPoseVector.leftEye.position).magnitude;
		float rightLength = (currentPoseVector.rightEar.position - currentPoseVector.rightEye.position).magnitude;

		bool isLookRight = false;
		if (currentPoseVector.leftEar.score >= accuracyThreshold && currentPoseVector.rightEar.score < 0.1) {
			isLookRight = true;
		} else if (leftLength > rightLength) {
			isLookRight = true;
		}
		float xRatio = isLookRight ? -(leftLength / rightLength) : rightLength / leftLength;

		Vector3 leftEyePosition  = animator.GetBoneTransform(HumanBodyBones.LeftEye).position;
		Vector3 rightEyePosition = animator.GetBoneTransform(HumanBodyBones.RightEye).position;
		float eyeLength = (leftEyePosition - rightEyePosition).magnitude;

		animator.SetLookAtWeight(1, 0.1f, 1f, 0.3f, 1f);
		Vector3 lookAtPosition = (leftEyePosition + rightEyePosition) / 2;
		if (Mathf.Abs(xRatio) >= 1.3f ) {
			lookAtPosition.x += eyeLength * xRatio;
		}
		animator.SetLookAtPosition(lookAtPosition);
	}

	private bool isEyesAndNoseScoreGood(PoseVector poseVector)
	{
		bool isGoodScore = true;
		string[] faceKeypoints = { "nose", "leftEye", "rightEye" };
		foreach (FieldInfo field in poseVector.GetType().GetFields())
		{
			if (!(faceKeypoints.Contains(field.Name))) continue;

			Keypoint keypoint = (Keypoint)field.GetValue(poseVector);
			if (keypoint.score < accuracyThreshold) {
				isGoodScore = false;
				break;
			}
		};
		return isGoodScore;
	}

	private bool isLeftOrRightEarScoreGood(PoseVector poseVector) {
		if (poseVector.leftEar.score >= accuracyThreshold)  { return true; }
		if (poseVector.rightEar.score >= accuracyThreshold) { return true; }
		return false;
	}

	private void updateArmsPosition()
	{
		// swap the left and right
		setWorldElbowPosition(currentPoseVector.leftElbow, AvatarIKHint.RightElbow);
		setWorldHandPosition(currentPoseVector.leftElbow, currentPoseVector.leftWrist, AvatarIKGoal.RightHand);

		setWorldElbowPosition(currentPoseVector.rightElbow, AvatarIKHint.LeftElbow);
		setWorldHandPosition(currentPoseVector.rightElbow, currentPoseVector.rightWrist, AvatarIKGoal.LeftHand);
	}

	private void setWorldElbowPosition(PartVector part, AvatarIKHint avatarPart)
	{
		if (part.score < accuracyThreshold) return;

		Vector3 position = part.position;
		position.z = cameraZIndex;
		Vector3 worldPosition = Camera.main.ScreenToWorldPoint(position);
		worldPosition.z = animator.GetIKHintPosition(avatarPart).z;

		animator.SetIKHintPositionWeight(avatarPart, 1);
		animator.SetIKHintPosition(avatarPart, worldPosition);
	}

	private void setWorldHandPosition(PartVector elbowPart, PartVector wristPart, AvatarIKGoal avatarPart)
	{
		if (wristPart.score < accuracyThreshold) return;

		Vector3 position = handPosition(elbowPart.position, wristPart.position);
		position.z = cameraZIndex;
		Vector3 worldPosition = Camera.main.ScreenToWorldPoint(position);
		worldPosition.z = animator.GetIKPosition(avatarPart).z;

		animator.SetIKPositionWeight(avatarPart, 1);
		animator.SetIKPosition(avatarPart, worldPosition);
	}

	private Vector3 handPosition(Vector3 elbowPosition, Vector3 wristPosition)
	{
		float armLength = (wristPosition - elbowPosition).magnitude;
		Vector3 handVector = (wristPosition - elbowPosition).normalized * armLength * 0.5f;
		return wristPosition + handVector;
	}

	public class PoseVector
	{
		public PartVector nose { get; set; }
		public PartVector leftEye { get; set; }
		public PartVector rightEye { get; set; }
		public PartVector leftEar { get; set; }
		public PartVector rightEar { get; set; }
		public PartVector leftShoulder { get; set; }
		public PartVector rightShoulder { get; set; }
		public PartVector leftElbow { get; set; }
		public PartVector rightElbow { get; set; }
		public PartVector leftWrist { get; set; }
		public PartVector rightWrist { get; set; }
		public PartVector leftHip { get; set; }
		public PartVector rightHip { get; set; }

		public PoseVector(Pose pose)
		{
			nose          = new PartVector(pose.nose);
			leftEye       = new PartVector(pose.leftEye);
			rightEye      = new PartVector(pose.rightEye);
			leftEar       = new PartVector(pose.leftEar);
			rightEar      = new PartVector(pose.rightEar);
			leftShoulder  = new PartVector(pose.leftShoulder);
			rightShoulder = new PartVector(pose.rightShoulder);
			leftElbow     = new PartVector(pose.leftElbow);
			rightElbow    = new PartVector(pose.rightElbow);
			leftWrist     = new PartVector(pose.leftWrist);
			rightWrist    = new PartVector(pose.rightWrist);
			leftHip       = new PartVector(pose.leftHip);
			rightHip      = new PartVector(pose.rightHip);
		}
	}

	public class PartVector
	{
		public float score { get; set; }
		public Vector3 position { get; set; }

		public PartVector(Keypoint keypoint)
		{
			score = keypoint.score;

			Vector3 originPose = new Vector3(poseCanvasWidth, poseCanvasHeight, 0);
			Vector3 partPose   = new Vector3(keypoint.x, keypoint.y, 0);
			position = originPose - partPose;
		}
	}

	[System.Serializable]
	public class CanvasSize
	{
		public int width;
		public int height;
	}

	[System.Serializable]
	public class Pose
	{
		public float score;
		public Keypoint nose;
		public Keypoint leftEye;
		public Keypoint rightEye;
		public Keypoint leftEar;
		public Keypoint rightEar;
		public Keypoint leftShoulder;
		public Keypoint rightShoulder;
		public Keypoint leftElbow;
		public Keypoint rightElbow;
		public Keypoint leftWrist;
		public Keypoint rightWrist;
		public Keypoint leftHip;
		public Keypoint rightHip;
		public Keypoint leftKnee;
		public Keypoint rightKnee;
		public Keypoint leftAnkle;
		public Keypoint rightAnkle;
	}

	[System.Serializable]
	public class Keypoint
	{
		public float score;
		public float x;
		public float y;
	}
}
