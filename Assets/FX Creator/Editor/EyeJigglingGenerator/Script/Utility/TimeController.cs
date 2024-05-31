using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TimeController
{
	float m_previousTime = 0;
	float m_currentTime = 0;
	
	public bool isPlay {get; private set;} = false;
	public float startTime {get;set;} = 0;
	public float endTime {get;set;} = 0;
	public bool Loop {get;set;}
	public float time {get => m_currentTime; set => m_currentTime = value;}
	public bool Return {get; private set;} = false;
	public float speed {get;set;} = 1f;
	public float deltaTime {get; private set;} = 0;
	
	public void Play() {isPlay = true;}
	
	public void Pause() {isPlay = false;}
	
	public void Stop() {isPlay = false; time = startTime;}
	
	public TimeController()
	{
		m_previousTime = (float)EditorApplication.timeSinceStartup;
	}
	
	public void EditorTime()
	{
		Return = false;
		float start_EditorTime = (float)EditorApplication.timeSinceStartup;
		if (isPlay)
		{
			deltaTime = start_EditorTime - m_previousTime;
			m_currentTime += (deltaTime < 0.1 ? deltaTime : 0) * speed;
			
			if (Loop)
			{
				Return = endTime <= m_currentTime ? true : false;
				m_currentTime = Mathf.Repeat(m_currentTime, endTime);
				m_currentTime = Mathf.Max(m_currentTime, startTime);
			}
		}
		m_previousTime = (float)EditorApplication.timeSinceStartup;
		time = m_currentTime;
	}
}
