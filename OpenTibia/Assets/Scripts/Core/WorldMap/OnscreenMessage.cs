﻿using System.Text.RegularExpressions;

namespace OpenTibiaUnity.Core.WorldMap
{
    public class OnscreenMessage
    {
        private static int s_NextID = 0;

        protected int m_TTL;
        protected int m_ID;
        protected int m_VisibleSince;
        protected int m_SpeakerLevel;
        protected string m_Speaker;
        protected MessageModes m_Mode;
        protected string m_Text;
        protected string m_RichText = null;

        public int VisibleSince {
            get { return m_VisibleSince; }
            set { m_VisibleSince = value; }
        }
        public int TTL {
            get { return m_TTL; }
            set { m_TTL = value; }
        }

        public string Text {
            get { return Text; }
        }
        public string RichText {
            get { return m_RichText; }
        }

        public OnscreenMessage(int statementID, string speaker, int speakerLevel, MessageModes mode, string text) {
            if (statementID <= 0)
                m_ID = --s_NextID;
            else
                m_ID = statementID;

            m_Speaker = speaker;
            m_SpeakerLevel = speakerLevel;
            m_Mode = mode;
            m_Text = text;
            m_VisibleSince = int.MaxValue;
            m_TTL = (30 + m_Text.Length / 3) * 100;
        }
        
        public void FormatMessage(string text, uint textARGB, uint highlightARGB) {
            m_RichText = StringHelper.RichTextSpecialChars(m_Text);
            if (m_Mode == MessageModes.NpcFrom)
                m_RichText = StringHelper.HighlightNpcTalk(m_RichText, highlightARGB);

            if (text != null)
                m_RichText = text + m_RichText;

            m_RichText = string.Format("<color=#{0:X6}>{1}</color>", textARGB, m_RichText);
        }
    }
}
