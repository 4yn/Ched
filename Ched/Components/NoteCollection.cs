﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ched.Components.Notes;

namespace Ched.Components
{
    /// <summary>
    /// ノーツを格納するコレクションを表すクラスです。
    /// </summary>
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class NoteCollection
    {
        [Newtonsoft.Json.JsonProperty]
        private List<Tap> taps;
        [Newtonsoft.Json.JsonProperty]
        private List<Hold> holds;
        [Newtonsoft.Json.JsonProperty]
        private List<Slide> slides;
        [Newtonsoft.Json.JsonProperty]
        private List<Air> airs;
        [Newtonsoft.Json.JsonProperty]
        private List<AirAction> airActions;
        [Newtonsoft.Json.JsonProperty]
        private List<Flick> flicks;
        [Newtonsoft.Json.JsonProperty]
        private List<Damage> damages;

        public List<Tap> Taps
        {
            get { return taps; }
            set { taps = value; }
        }

        public List<Hold> Holds
        {
            get { return holds; }
            set { holds = value; }
        }

        public List<Slide> Slides
        {
            get { return slides; }
            set { slides = value; }
        }

        public List<Air> Airs
        {
            get { return airs; }
            set { airs = value; }
        }

        public List<AirAction> AirActions
        {
            get { return airActions; }
            set { airActions = value; }
        }

        public List<Flick> Flicks
        {
            get { return flicks; }
            set { flicks = value; }
        }

        public List<Damage> Damages
        {
            get { return damages; }
            set { damages = value; }
        }

        public NoteCollection()
        {
            Taps = new List<Tap>();
            Holds = new List<Hold>();
            Slides = new List<Slide>();
            Airs = new List<Air>();
            AirActions = new List<AirAction>();
            Flicks = new List<Flick>();
            Damages = new List<Damage>();
        }

        public NoteCollection(UI.NoteView.NoteCollection collection)
        {
            Taps = collection.Taps.ToList();
            Holds = collection.Holds.ToList();
            Slides = collection.Slides.ToList();
            Airs = collection.Airs.ToList();
            AirActions = collection.AirActions.ToList();
            Flicks = collection.Flicks.ToList();
            Damages = collection.Damages.ToList();
        }
    }
}
