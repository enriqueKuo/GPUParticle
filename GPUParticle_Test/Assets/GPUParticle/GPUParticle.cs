using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ren.MaidChan
{
	public class GPUParticle : MonoBehaviour
	{
		[SerializeField]
		private Mesh _instanceMesh;

		private SkinnedMeshRenderer[] skinMeshs;

		[SerializeField]
		private Material material;

		[SerializeField]
		private Texture2D texture;

		[SerializeField]
		private ComputeShader computeShader;

		public float thresholdY1 = 0;
		public float thresholdY2 = 0;
		public float thresholdY3 = 0;

		[ColorUsage(true, true)]
		public Color color;

		private int subMeshIndex = 0;
		private const int WARP_SIZE = 1024;

		private int instanceCount;
		private int computeUpdate_KernelIndex;
		private int computeShaderExecuteTimes;

		private float maxY = float.MinValue;
		private float minY = float.MaxValue;

		private ComputeBuffer computeBuffer;
		private ComputeBuffer argsBuffer;

		private bool isShowEnd = true;

		public struct Particle
		{
			public Vector3 position;
			public float scale;
			public float height;
			public Color color;
			public Color color2;
			public float translate;
		}

		[Button]
		private void InitGPUParticle()
		{
			MaidController.Instance.StopAnim();
			skinMeshs = MaidController.Instance.GetMeshRenderers;

			instanceCount = 0;
			foreach (var item in skinMeshs)
			{
				instanceCount += item.sharedMesh.vertices.Length;
			}

			InitArgsBuffer();
		}

		[Button]
		private void ShowMaidButton()
		{
			if (computeBuffer == null)
			{
				StartCoroutine(ShowMaid());
			}
		}

		[Button]
		private void HideMaidButton()
		{
			if (computeBuffer == null)
			{
				StartCoroutine(HideMaid());
			}
		}

		private IEnumerator HideMaid()
		{
			isShowEnd = false;
			MaidController.Instance.StopAnim();
			InitComputeBuffer();

			MaidController.Instance.SetRenderer(true);
			bool complete = false;
			computeShader.SetVector("fcolor", new Vector4(color.r, color.g, color.b, color.a));
			thresholdY1 = maxY;
			thresholdY2 = maxY;
			thresholdY3 = maxY;
			DOTween.To(() => thresholdY1, x => thresholdY1 = x, minY, 1.0f).OnComplete(() =>
			{
				MaidController.Instance.SetRenderer(false);
			});
			DOTween.To(() => thresholdY2, x => thresholdY2 = x, minY, 1.0f).SetDelay(0.2f);
			DOTween.To(() => thresholdY3, x => thresholdY3 = x, minY, 2).SetDelay(0.4f).OnComplete(() =>
			{
				complete = true;
			});
			while (complete == false)
			{
				computeShader.SetFloat("deltaTime", Time.deltaTime);
				computeShader.SetFloat("threshold1", thresholdY1);
				Shader.SetGlobalFloat("_threshold", thresholdY1);
				computeShader.SetFloat("threshold2", thresholdY2);
				computeShader.SetFloat("threshold3", thresholdY3);

				computeShader.Dispatch(computeUpdate_KernelIndex, computeShaderExecuteTimes, 1, 1);
				Graphics.DrawMeshInstancedIndirect(
					_instanceMesh,
					subMeshIndex,
					material,
					new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)),
					argsBuffer);

				yield return null;
			}

			computeBuffer.Release();
			computeBuffer = null;
		}

		private IEnumerator ShowMaid()
		{
			isShowEnd = false;
			MaidController.Instance.StopAnim();
			InitComputeBuffer();

			bool complete = false;
			computeShader.SetVector("fcolor", new Vector4(color.r, color.g, color.b, color.a));
			thresholdY1 = minY;
			thresholdY2 = minY;
			thresholdY3 = minY;
			DOTween.To(() => thresholdY3, x => thresholdY3 = x, maxY, 2);
			DOTween.To(() => thresholdY2, x => thresholdY2 = x, maxY, 1.0f).SetDelay(0.9f).OnStart(() =>
			{
				MaidController.Instance.SetRenderer(true);
			});
			DOTween.To(() => thresholdY1, x => thresholdY1 = x, maxY, 1.0f).SetDelay(1.1f).OnComplete(() =>
			{
				complete = true;
			});

			while (complete == false)
			{
				Shader.SetGlobalFloat("_threshold", thresholdY1);
				computeShader.SetFloat("deltaTime", Time.deltaTime);
				computeShader.SetFloat("threshold1", thresholdY1);
				computeShader.SetFloat("threshold2", thresholdY2);
				computeShader.SetFloat("threshold3", thresholdY3);

				computeShader.Dispatch(computeUpdate_KernelIndex, computeShaderExecuteTimes, 1, 1);
				Graphics.DrawMeshInstancedIndirect(
					_instanceMesh,
					subMeshIndex,
					material,
					new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)),
					argsBuffer);

				yield return null;
			}

			MaidController.Instance.PlayAnim();
			computeBuffer.Release();
			computeBuffer = null;
		}

		private void InitArgsBuffer()
		{
			uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
			argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
			// Indirect args
			if (_instanceMesh != null)
			{
				args[0] = (uint)_instanceMesh.GetIndexCount(subMeshIndex); ;
				args[1] = (uint)instanceCount;
				args[2] = (uint)_instanceMesh.GetIndexStart(subMeshIndex);
				args[3] = (uint)_instanceMesh.GetBaseVertex(subMeshIndex);
			}
			else
			{
				args[0] = args[1] = args[2] = args[3] = 0;
			}
			argsBuffer.SetData(args);
		}

		private void InitComputeBuffer()
		{
			Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
			{
				Vector3 dir = point - pivot; // get point direction relative to pivot
				dir = Quaternion.Euler(angles) * dir; // rotate it
				point = dir + pivot; // calculate rotated point
				return point; // return it
			}

			computeShaderExecuteTimes = Mathf.CeilToInt((float)instanceCount / WARP_SIZE);
			int stride = Marshal.SizeOf(typeof(Particle));

			if (computeBuffer != null)
			{
				computeBuffer.Release();
			}
			computeBuffer = new ComputeBuffer(instanceCount, stride);

			Particle[] positions = new Particle[instanceCount];
			int index = 0;
			foreach (var item in skinMeshs)
			{
				var bakedMesh = new Mesh();
				item.BakeMesh(bakedMesh);
				for (int i = 0; i < bakedMesh.vertices.Length; i++)
				{
					positions[index].color = texture.GetPixelBilinear(bakedMesh.uv[i].x, bakedMesh.uv[i].y);
					positions[index].color2 = positions[index].color;
					positions[index].position = RotatePointAroundPivot(bakedMesh.vertices[i], new Vector3(0, 0, 0), item.transform.rotation.eulerAngles)
					 + transform.TransformPoint(item.transform.position);
					positions[index].scale = Random.Range(0.04f, 0.08f);
					positions[index].height = positions[index].scale;
					positions[index].translate = 0;

					var curY = positions[index].position.y;
					if (curY > maxY)
					{
						maxY = curY;
					}
					if (curY < minY)
					{
						minY = curY;
					}
					index++;
				}
			}
			minY -= 1;
			maxY += 1;

			computeBuffer.SetData(positions);
			computeUpdate_KernelIndex = computeShader.FindKernel("Update");

			computeShader.SetBuffer(computeUpdate_KernelIndex, "Particles", computeBuffer);
			material.SetBuffer("Particles", computeBuffer);
		}

		private void OnDisable()
		{
			if (computeBuffer != null)
			{
				computeBuffer.Release();
			}
			computeBuffer = null;

			if (argsBuffer != null)
			{
				argsBuffer.Release();
			}
			argsBuffer = null;
		}
	}
}