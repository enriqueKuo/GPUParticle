using UnityEngine;

namespace Ren.Common
{
	public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T _instance;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = (T)FindObjectOfType<T>();
				}
				return _instance;
			}
		}
	}
}