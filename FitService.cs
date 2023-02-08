using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Uni.Web
{
    public class FitService
    {
        private static Dictionary<string, object> DICT_EMPTY = new Dictionary<string, object>();
        const string url = "https://service.inll.com/ws.php";
        const string FIT_PG_PRE = "\"server\":\"main\",\"base\":\"fit\",\"login\":\"fit_points_dev\",\"pass\":\"zD9Xv93B3ja\",\"disable_json_escaping\":1";
        const string FOOT_PRE = "\"server\":\"inll.com\",\"base\":\"football\",\"login\":\"registrator\",\"pass\":\"343434u\"";
        const string HOCK_PRE = "\"server\":\"inll.com\",\"base\":\"hokkey\",\"login\":\"registrator\",\"pass\":\"343434u\"";

        private static List<int> CONTROL_TYPES = new List<int>()
        {
            2, 3, 4, 5, 8, 9,
            20, //центр кадра
            24, //game start
            25, //{ID=25 offset_L}]}
            26, //{ID=26 offset_R}]}
            27, //K1
            28, //K2
            29, //Focal
            30, //CamId
        };

		/// <summary>
		/// Базовый класс
		/// </summary>
		[DataContract()]
		public class MainServerApi
		{
			[DataMember(Name = "status")]
			public string Status { get; set; }
			[DataMember(Name = "error_code")]
			public int ErrorCode { get; set; }
			[DataMember(Name = "error_text")]
			public string ErrorMessage { get; set; }
			[DataMember(Name = "variables")]
			public Dictionary<string, object> Variables { get; set; }

			public bool IsSuccess => "Ok".Equals(Status);

			public virtual void ThrowIfErrorExists(string query, string responce, string function)
			{
				if (!IsSuccess)
					throw new MainServerApiException("");
			}
		}

		/// <summary>
		/// JSON ответ списком
		/// </summary>
		/// <typeparam name="T"></typeparam>
		[DataContract()]
		public class MainServerApi<T> : MainServerApi
		{
			[DataMember(Name = "data")]
			public List<T> Data { get; set; }
		}

		[DataContract()]
		public class MainServerApiSingle<T> : MainServerApi
		{
			[DataMember(Name = "data")]
			public T Data { get; set; }
		}
		
		/// <summary>
		/// JSON ответ авторизации
		/// </summary>
		[DataContract()]
		public class AuthJson
		{
			[DataMember(Name = "result")]
			public int? Result { get; set; }
			[DataMember(Name = "experience")]
			public int Experience { get; set; }
		}
		
        [DataContract]
        public class GetReferencePointsRequest
        {
            [DataMember(Name = "sport_id")]
            public int SportId { get; set; }

            [DataMember(Name = "match_id")]
            public int MatchId { get; set; }

            [DataMember(Name = "half")]
            public int HalfIndex { get; set; }

            [DataMember(Name = "frame_start")]
            public int FrameStart { get; set; }

            [DataMember(Name = "frame_end")]
            public int FrameEnd { get; set; }
        }

        [DataContract]
        public class ReferencePointData : GetReferencePointsRequest
        {
            [DataMember(Name = "frame")]
            public int FrameNumber { get; set; }

            [DataMember(Name = "data")]
            public List<ReferencePoint> Data { get; set; }
        }

        [DataContract]
        public class ReferencePointIUD : ReferencePointData
        {
            [DataMember(Name = "iud")]
            public int Iud { get; set; }
        }

        [DataContract]
        public class FuncAskFReferencePoint<T>
        {
            [DataMember(Name = "ask_f_reference_point")]
            private string FuncRow { get; set; }

            public List<T> Func { get; set; }

            [OnDeserialized]
            void OnDeserializing(StreamingContext context)
            {
                if (!string.IsNullOrEmpty(FuncRow))
                {
                    FuncRow = FuncRow.Replace("\\", "");
                    Func = JsonHelper.Parse<List<T>>(FuncRow);
                }
            }
        }

        [DataContract]
        public class ReturnTimeCode
        {
            [DataMember(Name = "video_timecode")]
            public string TimeCode { get; set; }
        }

        [DataContract]
        public class ReturnStatus
        {
            [DataMember(Name = "result")]
            public string Status { get; set; }
            [DataMember(Name = "message")]
            public string Message { get; set; }
        }

        [DataContract]
        public class FuncSaveReferencePoint<T>
        {
            [DataMember(Name = "save_reference_point")]
            private string FuncRow { get; set; }

            public T Result { get; set; }

            [OnDeserialized]
            void OnDeserializing(StreamingContext context)
            {
                if (!string.IsNullOrEmpty(FuncRow))
                {
                    Result = JsonHelper.Parse<T>(FuncRow);
                }
            }
        }

        [DataContract]
        public class DefiningPointDto
        {
            [DataMember(Name = "id")]
            public int Id { get; set; }
            [DataMember(Name = "data_value_int_1")]
            public int? Int1 { get; set; }
            [DataMember(Name = "data_value_int_2")]
            public int? Int2 { get; set; }
            [DataMember(Name = "data_value_float_1")]
            public float? Float1 { get; set; }
            [DataMember(Name = "data_value_float_2")]
            public float? Float2 { get; set; }
            [DataMember(Name = "c_mileage_datatype")]
            public int CPType { get; set; }
            [DataMember(Name = "video_timecode")]
            public string TimeCode { get; set; }
            public int HaldIndex { get; set; }

            public DefiningPointDto() { }

            public DefiningPointDto(DefiningPoint defPt)
            {
                Id = defPt.Id;
                Int1 = defPt.Int1;
                Int2 = defPt.Int2;
                Float1 = defPt.Float1;
                Float2 = defPt.Float2;
                CPType = defPt.CPType;
                TimeCode = TimeCodeHelper.ConvertMilisecondsToTimeCode(defPt.TimeCode);
            }

            public DefiningPoint CreateModel()
            {
                return new DefiningPoint
                {
                    Id = Id,
                    Int1 = Int1,
                    Int2 = Int2,
                    Float1 = Float1,
                    Float2 = Float2,
                    CPType = CPType,
                    TimeCode = TimeCodeHelper.ConvertTimeCodeToMiliseconds(TimeCode)
                };
            }
        }

        public static List<DefiningPointDescr> GetDefiningPointDescrList()
        {
            var serverRequest = new MainServerRequest(FOOT_PRE, "lst_c_mileage_datatype", DICT_EMPTY, DICT_EMPTY);
            var req = Request<MainServerApi<DefiningPointDescr>>(serverRequest);

            return req.Data;
        }

        public static List<int> GetTimeCodesList(int matchId, int halfIndex)
        {
            var dict = new Dictionary<string, object>
            {
                { "match_id", matchId },
                { "half", halfIndex },
            };
            var serverRequest = new MainServerRequest(FOOT_PRE, "ask_f_mileage_matchdata_timecodes", dict, DICT_EMPTY);
            var req = Request<MainServerApi<ReturnTimeCode>>(serverRequest);

            return req.Data.Select(o => TimeCodeHelper.ConvertTimeCodeToMiliseconds(o.TimeCode)).ToList();
        }

        public static List<DefiningPoint> GetControlPoints(int sportId, int matchId, int halfIndex, int? timecode = null)
        {
            switch (sportId)
            {
                case 0:
                    return GetControlPointsSoccer(matchId, halfIndex, timecode);

                case 1:
                    return GetControlPointsHockey(matchId, halfIndex, timecode);

                default:
                    throw new Exception($"The sport type={sportId} is unable in the version!");

            }
        }

        public static List<DefiningPoint> GetControlPointsSoccer(int matchId, int halfIndex, int? timecode = null)
        {
            var dict = new Dictionary<string, object>
            {
                { "match_id", matchId },
                { "half", halfIndex },
            };

            if (timecode.HasValue)
                dict.Add("video_timecode", TimeCodeHelper.ConvertMilisecondsToTimeCode(timecode.Value));

            var serverRequest = new MainServerRequest(FOOT_PRE, "ask_f_mileage_matchdata_2", dict, DICT_EMPTY);
            var req = Request<MainServerApi<DefiningPointDto>>(serverRequest);

            return req.Data.Select(o => o.CreateModel()).ToList();
        }

        public static List<DefiningPoint> GetControlPointsHockey(int matchId, int halfIndex, int? timecode = null)
        {
            var dict = new Dictionary<string, object>
            {
                { "match_id", matchId },
                { "half", halfIndex },
            };

            if (timecode.HasValue)
                dict.Add("video_timecode", TimeCodeHelper.ConvertMilisecondsToTimeCode(timecode.Value));

            var serverRequest = new MainServerRequest(HOCK_PRE, "ask_f_mileage_matchdata", dict, DICT_EMPTY);
            var req = Request<MainServerApi<DefiningPointDto>>(serverRequest);

            return req.Data.Select(o => o.CreateModel()).ToList();
        }

        public static void SaveControlPoints(
            int sportId,
            int matchId,
            int halfIndex,
            List<DefiningPoint> dpts,
            CancellationToken token = default,
            bool remove = false)
        {
            switch (sportId)
            {
                case 0:
                    SaveControlPointsSoccer(matchId, halfIndex, dpts, token, remove);
                    break;

                case 1:
                    SaveControlPointsHockey(matchId, halfIndex, dpts, token, remove);
                    break;

                default:
                    throw new Exception($"The sport type={sportId} is unable in the version!");
            }
        }

        public static void SaveFieldPoints(
            string server,
            Match match,
            int halfIndex,
            Team team,
            TargetObject to,
            List<FieldPoint> fpl)
        {
            foreach (var fp in fpl)
            {
                var iud = 1;

                var datain = new Dictionary<string, object>
                {
                    { "action", iud },
                    { "match_id", match.Id },
                    { "period", halfIndex },
                    { "team_id", team != null ? team.Id : 0 },
                    { "player_id", to.Id },
                    { "video_filename", "" },
                    { "video_part", fp.Side == Side.L ? 1 : 2 },
                    { "video_second", fp.Time / 100 },
                    { "pos_x", 0 },
                    { "pos_y", 0 },
                    { "with_ball", 1 },
                    { "pos_x_recalc", Math.Round(fp.Pos2D.X, 2) },
                    { "pos_y_recalc", Math.Round(fp.Pos2D.Y, 2) },
                    { "distance", fp.Distance },
                    { "speed", Math.Round(double.IsInfinity(fp.Speed) ? 0.0 : fp.Speed, 2) },
                    { "acceleration", double.IsInfinity(fp.Acceleration) ? 0.0 : fp.Acceleration },
                    { "metabolic_power", double.IsInfinity(fp.MetabolicPower) ? 0.0 : fp.MetabolicPower },
                    { "energy_cost", double.IsInfinity(fp.EnergyCost) ? 0.0 : fp.EnergyCost },
                    { "estimated_energy", double.IsInfinity(fp.EstimatedEnergy) ? 0.0 : fp.EstimatedEnergy },
                    { "c_mileage_event", fp.Type == MarkerTypeEnum.Point ? 0 : Convert.ToInt32(fp.Type) },
                    { "player2_id", 0 },
                    { "user_id", 1012 },
                };

                var dataout = new Dictionary<string, object>
                {
                    {"ret", "int"},
                    {"error_text", "varchar(50)"},
                };

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var serverRequest = new MainServerRequest(FOOT_PRE, "iud_f_mileage_data3", datain, dataout);
                        var req = Request<MainServerApi>(serverRequest);

                        if (req != null 
							&& req.Variables != null 
							&& req.Variables.ContainsKey("@ret") 
							&& int.TryParse(req.Variables["@ret"]?.ToString(), out int id))
                        {
                            if (id != -1)
                            {
                                fp.Id = id;
                                break;
                            }
                            else
                            {
                                throw new Exception(req.Variables["@error_text"]?.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (i == 2)
                            throw;

                        Thread.Sleep(5000);
                    }
                }
            }
        }

        public static void SaveFieldPointsXml(
            Match match,
            int halfIndex,
            Team team,
            TargetObject to,
            IEnumerable<DbSaveAction> fpl,
            int userId)
        {
            var sb = new StringBuilder();
            sb.Append("<root>");
            foreach (var action in fpl)
            {
                var fp = action.Point;
                var teamId = team != null ? team.Id : 0;
                var videoPart = fp.Side == Side.L ? 1 : 2;
                var cMileageEvent = fp.Type == MarkerTypeEnum.Point ? 0 : Convert.ToInt32(fp.Type);

                sb.Append("<Row>");
                sb.Append($"<action>{(int)action.Mode}</action>");
                sb.Append($"<match_id>{match.Id}</match_id>");
                sb.Append($"<period>{halfIndex}</period>");
                sb.Append($"<team_id>{teamId}</team_id>");
                sb.Append($"<player_id>{to.Id}</player_id>");
                sb.Append($"<video_filename></video_filename>");
                sb.Append($"<video_part>{videoPart}</video_part>");
                sb.Append($"<video_second>{fp.Time / 100}</video_second>");
                sb.Append($"<pos_x>{fp.Pos.X}</pos_x>");
                sb.Append($"<pos_y>{fp.Pos.Y}</pos_y>");
                sb.Append($"<with_ball>{1}</with_ball>");
                sb.Append($"<pos_x_recalc>{Math.Round(fp.Pos2D.X, 2).ToString(CultureInfo.InvariantCulture)}</pos_x_recalc>");
                sb.Append($"<pos_y_recalc>{Math.Round(fp.Pos2D.Y, 2).ToString(CultureInfo.InvariantCulture)}</pos_y_recalc>");
                sb.Append($"<distance>{fp.Distance.ToString(CultureInfo.InvariantCulture)}</distance>");
                sb.Append($"<speed>{(double.IsInfinity(fp.Speed) ? 0.0 : fp.Speed).ToString(CultureInfo.InvariantCulture)}</speed>");
                sb.Append($"<acceleration>{(double.IsInfinity(fp.Acceleration) ? 0.0 : fp.Acceleration).ToString(CultureInfo.InvariantCulture)}</acceleration>");
                sb.Append($"<metabolic_power>{(double.IsInfinity(fp.MetabolicPower) ? 0.0 : fp.MetabolicPower).ToString(CultureInfo.InvariantCulture)}</metabolic_power>");
                sb.Append($"<energy_cost>{(double.IsInfinity(fp.EnergyCost) ? 0.0 : fp.EnergyCost).ToString(CultureInfo.InvariantCulture)}</energy_cost>");
                sb.Append($"<estimated_energy>{(double.IsInfinity(fp.EstimatedEnergy) ? 0.0 : fp.EstimatedEnergy).ToString(CultureInfo.InvariantCulture)}</estimated_energy>");
                sb.Append($"<c_mileage_event>{cMileageEvent}</c_mileage_event>");
                sb.Append($"<player2_id>{fp.Player2Id}</player2_id>");
                sb.Append($"<user_id>{userId}</user_id>");
                sb.Append("</Row>");
            }
            sb.Append("</root>");

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var serverRequest = new MainServerRequest(FOOT_PRE, "iud_f_mileage_data33", 
                        new Dictionary<string, object>
                        {
                            { "xml_data", sb.ToString() }
                        },
                        new Dictionary<string, object>
                        {
                            {"ret", "int"},
                            {"error_text", "varchar(50)"},
                        });

                    var req = Request<MainServerApi>(serverRequest);

                    if (req != null 
						&& req.Variables != null 
						&& req.Variables.ContainsKey("@ret") 
						&& int.TryParse(req.Variables["@ret"]?.ToString(), out int id))
                    {
                        if (id != -1)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception(req.Variables["@error_text"]?.ToString());
                        }
                    }
                }
                catch (Exception)
                {
                    if (i == 2)
                        throw;

                    Thread.Sleep(5000);
                }
            }
        }

        public static void SaveControlPointsSoccer(
            int matchId, 
            int halfIndex,
            List<DefiningPoint> dpts,
            CancellationToken token = default, 
            bool remove = false)
        {
            Parallel.ForEach(dpts, 
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, 
                dp =>
            {
                var iud = dp.Id > 0 ? 2 : 1;
                if (remove)
                    iud = 3;

                var datain = new Dictionary<string, object>
                {
                    { "action", iud },
                    { "c_mileage_datatype", dp.CPType },
                    { "id", dp.Id },
                    { "match_id", matchId },
                    { "half", halfIndex },
                    { "video_timecode", TimeCodeHelper.ConvertMilisecondsToTimeCode(dp.TimeCode) },
                    { "data_value_int_1", dp.Int1 },
                    { "data_value_int_2", dp.Int2 },
                    { "data_value_float_1", null },
                    { "data_value_float_2",null },
                    { "user_id", User.Current.Id },
                };

                if (!string.IsNullOrEmpty(dp.CamName))
                {
                    datain.Add("data_value_varchar", dp.CamName);
                }

                var dataout = new Dictionary<string, object>
                {
                    {"ret", "int"},
                    {"error_text", "varchar(50)"},
                };

                var serverRequest = new MainServerRequest(FOOT_PRE, "iud_f_mileage_matchdata_4", datain, dataout);
                var req = Request<MainServerApi>(serverRequest);

                if (req != null 
					&& req.Variables != null 
					&& req.Variables.ContainsKey("@ret") 
					&& int.TryParse(req.Variables["@ret"]?.ToString(), out int id))
                {
                    if (id > 0)
                        dp.Id = id;
                    else
                        throw new Exception(req.Variables["@error_text"]?.ToString());
                }
            });
        }

        public static void SaveControlPointsHockey(
            int matchId,
            int halfIndex,
            List<DefiningPoint> dpts,
            CancellationToken token = default,
            bool remove = false)
        {
            Parallel.ForEach(dpts,
                new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = token },
                dp =>
                {
                    var iud = dp.Id > 0 ? 2 : 1;
                    if (remove)
                        iud = 3;

					//int uid = Convert.ToInt32(fp.Status) + 1;

					var datain = new Dictionary<string, object>() {
						{ "action", iud },
						{ "c_mileage_datatype", dp.CPType },
						{ "match_id", matchId },
						{ "half", halfIndex },
						{ "video_timecode", TimeCodeHelper.ConvertMilisecondsToTimeCode(dp.TimeCode) },
						{ "data_value_int_1", dp.Int1.HasValue ? dp.Int1.Value : 0 },
						{ "data_value_int_2", dp.Int2.HasValue ? dp.Int2.Value : 0 },
						{ "user_id", User.Current.Id },
						{ "reserv_variant", 0 },
					};

                    // request and parse
                    var serverRequest = new MainServerRequest(HOCK_PRE, "iud_f_mileage_matchdata", datain, DICT_EMPTY) { DbType = DbTypeEnum.Postgre };
                    var req = Request<MainServerApi>(serverRequest);

                    if (req != null && req.Variables != null && req.Variables.ContainsKey("@ret") && int.TryParse(req.Variables["@ret"]?.ToString(), out int id))
                    {
                        if (id > 0)
                            dp.Id = id;
                        else
                            throw new Exception(req.Variables["@error_text"]?.ToString());
                    }
                });
        }

        public static void SaveReferencePoints<T>(List<ReferencePointIUD> points)
        {
            var json = JsonHelper.GetJson(points);
            json = json.Replace("\"", "\\\"");
            json = json.Replace("\\\\", "\\");
            json = "\"" + json + "\"";

            try
            {
                var datain = new Dictionary<string, object>
                {
                    { "points", json },
                };

                var serverRequest = new MainServerRequest(FIT_PG_PRE, "save_reference_point", datain, DICT_EMPTY);
                serverRequest.DbType = DbTypeEnum.Postgre;

                var res = Request<MainServerApiSingle<ReturnStatus>>(serverRequest);

                if (!res.IsSuccess)
                {
                    Log.Write(res.ErrorCode + "\n" + res.ErrorMessage);
                    throw new Exception(res.ErrorCode + "\n" + res.ErrorMessage);
                }

                if (!res.Data.Status.Equals("Ok"))
                {
                    Log.Write(res.Data.Message);
                    throw new Exception(res.Data.Message);
                }
            }
            catch (Exception ex)
            {
                ex.InjectInfoAndThrow($"{url} {json}");
                throw;
            }
        }

        public static List<ReferencePointData> GetReferencePoints(int sportId, int matchId, int halfIndex, int frameStart, int frameEnd)
        {
            //{"server":"main","base":"fitness","login":"registrator","pass":"u","proc":"ask_f_reference_point","params":{"_p_params":"{\"half\":1,\"match_id\":123,\"sport_id\":1}"}}
            //{"server":"main","base":"fitness","login":"registrator","pass":"u","proc":"ask_f_reference_point","params":{"_p_params":"{\"half\":1,\"match_id\":123,\"sport_id\":1}"}}
            var req = new GetReferencePointsRequest
            {
                SportId = sportId,
                MatchId = matchId,
                HalfIndex = halfIndex,
                FrameStart = frameStart,
                FrameEnd = frameEnd,
            };

            var json = JsonHelper.GetJson(req);
            json = json.Replace("\"", "\\\"");
            json = json.Replace("\\\\", "\\");
            json = "\"" + json + "\"";

            try
            {
                var datain = new Dictionary<string, object>
                {
                    { "params", json },
                };

                var serverRequest = new MainServerRequest(FIT_PG_PRE, "ask_f_reference_point", datain, DICT_EMPTY);
                serverRequest.DbType = DbTypeEnum.Postgre;

                var res = Request<MainServerApi<ReferencePointData>>(serverRequest);

                if (!res.IsSuccess)
                {
                    Log.Write(res.ErrorCode + "\n" + res.ErrorMessage);
                    throw new Exception(res.ErrorCode + "\n" + res.ErrorMessage);
                }

                return res.Data;
            }
            catch (Exception ex)
            {
                ex.InjectInfoAndThrow($"{url} {json}");
                throw;
            }
        }

	
		private static T Request<T>(MainServerRequest serverRequest)
        {
            const Method method = datain.Count == 0 && dataout.Count == 0 ? Method.GET : Method.POST;

            const string url = "https://service.inll.com/ws.php";

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = serverRequest.Timeout;
            request.Method = method.ToString();
            request.ContentType = "application/json";
            request.Host = "service.inll.com";
            request.ServicePoint.ConnectionLimit = 50;

            var r = serverRequest.GetRequestData();
            var encodedPostParams = Encoding.UTF8.GetBytes(r);
            request.ContentLength = encodedPostParams.Length;
            request.GetRequestStream().Write(encodedPostParams, 0, encodedPostParams.Length);
            request.GetRequestStream().Close();

            Log.Write(r.Replace("Bu", "***"));

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (var strm = response.GetResponseStream() 
                    ?? throw new InvalidOperationException())
                {
                    var res = JsonHelper.Parse<T>(strm);
                    if (serverRequest.WriteResponseLog)
                    {
                        var r2 = JsonHelper.GetJson(res).Replace("Bu", "***");
                        Log.Write(r2);
                    }

                    return res;
                }
            }
            catch (Exception ex)
            {
                ex.InjectInfoAndThrow(url + " proc=" + serverRequest.Proc + ": ");
                throw;
            }
        }
        private static async Task<T> RequestAsync<T>(MainServerRequest serverRequest)
        {
            const Method method = datain.Count == 0 && dataout.Count == 0 ? Method.GET : Method.POST;

            const string url = "https://service.inll.com/ws.php";

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = serverRequest.Timeout;
            request.Method = method.ToString();
            request.ContentType = "application/json";
            request.Host = "service.inll.com";

            var r = serverRequest.GetRequestData();
            var encodedPostParams = Encoding.UTF8.GetBytes(r);
            request.ContentLength = encodedPostParams.Length;
            request.GetRequestStream().Write(encodedPostParams, 0, encodedPostParams.Length);
            request.GetRequestStream().Close();

            try
            {
                var response = (HttpWebResponse)await Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                using (var strm = response.GetResponseStream() ?? throw new InvalidOperationException())
                {
                    //var s = new StreamReader(strm).ReadToEnd();
                    return JsonHelper.Parse<T>(strm);
                }
            }
            catch (Exception ex)
            {
                ex.InjectInfoAndThrow(url + " proc=" + serverRequest.Proc + ": ");
                throw;
            }
        }
		
    }
}
