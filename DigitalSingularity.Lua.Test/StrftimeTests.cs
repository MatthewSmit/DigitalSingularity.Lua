namespace DigitalSingularity.Lua.Test;

using System.Globalization;
using System.Text;

public class StrftimeTests
{
    private const long LeapDay = 951865507;
    private const long UnixEpoch = 0;
    private const long SundayYearEnd = 1704028455;
    private const long IsoWeekYearBoundary = 1451606462;

    [TestCase("%a", "C", LeapDay, "Tue")]
    [TestCase("%a", "C", UnixEpoch, "Thu")]
    [TestCase("%a", "C", SundayYearEnd, "Sun")]
    [TestCase("%a", "C", IsoWeekYearBoundary, "Fri")]
    [TestCase("%a", "en-GB", LeapDay, "Tue")]
    [TestCase("%a", "en-GB", UnixEpoch, "Thu")]
    [TestCase("%a", "en-GB", SundayYearEnd, "Sun")]
    [TestCase("%a", "en-GB", IsoWeekYearBoundary, "Fri")]
    [TestCase("%a", "ja-JP", LeapDay, "火")]
    [TestCase("%a", "ja-JP", UnixEpoch, "木")]
    [TestCase("%a", "ja-JP", SundayYearEnd, "日")]
    [TestCase("%a", "ja-JP", IsoWeekYearBoundary, "金")]
    [TestCase("%a", "es-ES", LeapDay, "mar")]
    [TestCase("%a", "es-ES", UnixEpoch, "jue")]
    [TestCase("%a", "es-ES", SundayYearEnd, "dom")]
    [TestCase("%a", "es-ES", IsoWeekYearBoundary, "vie")]
    [TestCase("%a", "ar-SA", LeapDay, "الثلاثاء")]
    [TestCase("%a", "ar-SA", UnixEpoch, "الخميس")]
    [TestCase("%a", "ar-SA", SundayYearEnd, "الأحد")]
    [TestCase("%a", "ar-SA", IsoWeekYearBoundary, "الجمعة")]
    public void TestStrftime_a(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }
    
    [TestCase("%A", "C", LeapDay, "Tuesday")]
    [TestCase("%A", "C", UnixEpoch, "Thursday")]
    [TestCase("%A", "C", SundayYearEnd, "Sunday")]
    [TestCase("%A", "C", IsoWeekYearBoundary, "Friday")]
    [TestCase("%A", "en-GB", LeapDay, "Tuesday")]
    [TestCase("%A", "en-GB", UnixEpoch, "Thursday")]
    [TestCase("%A", "en-GB", SundayYearEnd, "Sunday")]
    [TestCase("%A", "en-GB", IsoWeekYearBoundary, "Friday")]
    [TestCase("%A", "ja-JP", LeapDay, "火曜日")]
    [TestCase("%A", "ja-JP", UnixEpoch, "木曜日")]
    [TestCase("%A", "ja-JP", SundayYearEnd, "日曜日")]
    [TestCase("%A", "ja-JP", IsoWeekYearBoundary, "金曜日")]
    [TestCase("%A", "es-ES", LeapDay, "martes")]
    [TestCase("%A", "es-ES", UnixEpoch, "jueves")]
    [TestCase("%A", "es-ES", SundayYearEnd, "domingo")]
    [TestCase("%A", "es-ES", IsoWeekYearBoundary, "viernes")]
    [TestCase("%A", "ar-SA", LeapDay, "الثلاثاء")]
    [TestCase("%A", "ar-SA", UnixEpoch, "الخميس")]
    [TestCase("%A", "ar-SA", SundayYearEnd, "الأحد")]
    [TestCase("%A", "ar-SA", IsoWeekYearBoundary, "الجمعة")]
    public void TestStrftime_A(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%b", "C", LeapDay, "Feb")]
    [TestCase("%b", "C", UnixEpoch, "Jan")]
    [TestCase("%b", "C", SundayYearEnd, "Dec")]
    [TestCase("%b", "C", IsoWeekYearBoundary, "Jan")]
    [TestCase("%b", "en-GB", LeapDay, "Feb")]
    [TestCase("%b", "en-GB", UnixEpoch, "Jan")]
    [TestCase("%b", "en-GB", SundayYearEnd, "Dec")]
    [TestCase("%b", "en-GB", IsoWeekYearBoundary, "Jan")]
    [TestCase("%b", "ja-JP", LeapDay, "2月")]
    [TestCase("%b", "ja-JP", UnixEpoch, "1月")]
    [TestCase("%b", "ja-JP", SundayYearEnd, "12月")]
    [TestCase("%b", "ja-JP", IsoWeekYearBoundary, "1月")]
    [TestCase("%b", "es-ES", LeapDay, "feb")]
    [TestCase("%b", "es-ES", UnixEpoch, "ene")]
    [TestCase("%b", "es-ES", SundayYearEnd, "dic")]
    [TestCase("%b", "es-ES", IsoWeekYearBoundary, "ene")]
    [TestCase("%b", "ar-SA", LeapDay, "فبراير")]
    [TestCase("%b", "ar-SA", UnixEpoch, "يناير")]
    [TestCase("%b", "ar-SA", SundayYearEnd, "ديسمبر")]
    [TestCase("%b", "ar-SA", IsoWeekYearBoundary, "يناير")]
    public void TestStrftime_b(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%B", "C", LeapDay, "February")]
    [TestCase("%B", "C", UnixEpoch, "January")]
    [TestCase("%B", "C", SundayYearEnd, "December")]
    [TestCase("%B", "C", IsoWeekYearBoundary, "January")]
    [TestCase("%B", "en-GB", LeapDay, "February")]
    [TestCase("%B", "en-GB", UnixEpoch, "January")]
    [TestCase("%B", "en-GB", SundayYearEnd, "December")]
    [TestCase("%B", "en-GB", IsoWeekYearBoundary, "January")]
    [TestCase("%B", "ja-JP", LeapDay, "2月")]
    [TestCase("%B", "ja-JP", UnixEpoch, "1月")]
    [TestCase("%B", "ja-JP", SundayYearEnd, "12月")]
    [TestCase("%B", "ja-JP", IsoWeekYearBoundary, "1月")]
    [TestCase("%B", "es-ES", LeapDay, "febrero")]
    [TestCase("%B", "es-ES", UnixEpoch, "enero")]
    [TestCase("%B", "es-ES", SundayYearEnd, "diciembre")]
    [TestCase("%B", "es-ES", IsoWeekYearBoundary, "enero")]
    [TestCase("%B", "ar-SA", LeapDay, "فبراير")]
    [TestCase("%B", "ar-SA", UnixEpoch, "يناير")]
    [TestCase("%B", "ar-SA", SundayYearEnd, "ديسمبر")]
    [TestCase("%B", "ar-SA", IsoWeekYearBoundary, "يناير")]
    public void TestStrftime_B(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%c", "C", LeapDay, "Tuesday, 29 February 2000 23:05:07")]
    [TestCase("%c", "C", UnixEpoch, "Thursday, 01 January 1970 00:00:00")]
    [TestCase("%c", "C", SundayYearEnd, "Sunday, 31 December 2023 13:14:15")]
    [TestCase("%c", "C", IsoWeekYearBoundary, "Friday, 01 January 2016 00:01:02")]
    [TestCase("%c", "en-GB", LeapDay, "Tuesday, 29 February 2000 23:05:07")]
    [TestCase("%c", "en-GB", UnixEpoch, "Thursday, 1 January 1970 00:00:00")]
    [TestCase("%c", "en-GB", SundayYearEnd, "Sunday, 31 December 2023 13:14:15")]
    [TestCase("%c", "en-GB", IsoWeekYearBoundary, "Friday, 1 January 2016 00:01:02")]
    [TestCase("%c", "ja-JP", LeapDay, "2000年2月29日火曜日 23:05:07")]
    [TestCase("%c", "ja-JP", UnixEpoch, "1970年1月1日木曜日 0:00:00")]
    [TestCase("%c", "ja-JP", SundayYearEnd, "2023年12月31日日曜日 13:14:15")]
    [TestCase("%c", "ja-JP", IsoWeekYearBoundary, "2016年1月1日金曜日 0:01:02")]
    [TestCase("%c", "es-ES", LeapDay, "martes, 29 de febrero de 2000 23:05:07")]
    [TestCase("%c", "es-ES", UnixEpoch, "jueves, 1 de enero de 1970 0:00:00")]
    [TestCase("%c", "es-ES", SundayYearEnd, "domingo, 31 de diciembre de 2023 13:14:15")]
    [TestCase("%c", "es-ES", IsoWeekYearBoundary, "viernes, 1 de enero de 2016 0:01:02")]
    [TestCase("%c", "ar-SA", LeapDay, "الثلاثاء، 29 فبراير 2000 م 11:05:07 م")]
    [TestCase("%c", "ar-SA", UnixEpoch, "الخميس، 1 يناير 1970 م 12:00:00 ص")]
    [TestCase("%c", "ar-SA", SundayYearEnd, "الأحد، 31 ديسمبر 2023 م 1:14:15 م")]
    [TestCase("%c", "ar-SA", IsoWeekYearBoundary, "الجمعة، 1 يناير 2016 م 12:01:02 ص")]
    public void TestStrftime_c(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%C", "C", LeapDay, "20")]
    [TestCase("%C", "C", UnixEpoch, "19")]
    [TestCase("%C", "C", SundayYearEnd, "20")]
    [TestCase("%C", "C", IsoWeekYearBoundary, "20")]
    [TestCase("%C", "en-GB", LeapDay, "20")]
    [TestCase("%C", "en-GB", UnixEpoch, "19")]
    [TestCase("%C", "en-GB", SundayYearEnd, "20")]
    [TestCase("%C", "en-GB", IsoWeekYearBoundary, "20")]
    [TestCase("%C", "ja-JP", LeapDay, "20")]
    [TestCase("%C", "ja-JP", UnixEpoch, "19")]
    [TestCase("%C", "ja-JP", SundayYearEnd, "20")]
    [TestCase("%C", "ja-JP", IsoWeekYearBoundary, "20")]
    [TestCase("%C", "es-ES", LeapDay, "20")]
    [TestCase("%C", "es-ES", UnixEpoch, "19")]
    [TestCase("%C", "es-ES", SundayYearEnd, "20")]
    [TestCase("%C", "es-ES", IsoWeekYearBoundary, "20")]
    [TestCase("%C", "ar-SA", LeapDay, "20")]
    [TestCase("%C", "ar-SA", UnixEpoch, "19")]
    [TestCase("%C", "ar-SA", SundayYearEnd, "20")]
    [TestCase("%C", "ar-SA", IsoWeekYearBoundary, "20")]
    public void TestStrftime_C(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%d", "C", LeapDay, "29")]
    [TestCase("%d", "C", UnixEpoch, "01")]
    [TestCase("%d", "C", SundayYearEnd, "31")]
    [TestCase("%d", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%d", "en-GB", LeapDay, "29")]
    [TestCase("%d", "en-GB", UnixEpoch, "01")]
    [TestCase("%d", "en-GB", SundayYearEnd, "31")]
    [TestCase("%d", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%d", "ja-JP", LeapDay, "29")]
    [TestCase("%d", "ja-JP", UnixEpoch, "01")]
    [TestCase("%d", "ja-JP", SundayYearEnd, "31")]
    [TestCase("%d", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%d", "es-ES", LeapDay, "29")]
    [TestCase("%d", "es-ES", UnixEpoch, "01")]
    [TestCase("%d", "es-ES", SundayYearEnd, "31")]
    [TestCase("%d", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%d", "ar-SA", LeapDay, "29")]
    [TestCase("%d", "ar-SA", UnixEpoch, "01")]
    [TestCase("%d", "ar-SA", SundayYearEnd, "31")]
    [TestCase("%d", "ar-SA", IsoWeekYearBoundary, "01")]
    public void TestStrftime_d(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%D", "C", LeapDay, "02/29/00")]
    [TestCase("%D", "C", UnixEpoch, "01/01/70")]
    [TestCase("%D", "C", SundayYearEnd, "12/31/23")]
    [TestCase("%D", "C", IsoWeekYearBoundary, "01/01/16")]
    [TestCase("%D", "en-GB", LeapDay, "02/29/00")]
    [TestCase("%D", "en-GB", UnixEpoch, "01/01/70")]
    [TestCase("%D", "en-GB", SundayYearEnd, "12/31/23")]
    [TestCase("%D", "en-GB", IsoWeekYearBoundary, "01/01/16")]
    [TestCase("%D", "ja-JP", LeapDay, "02/29/00")]
    [TestCase("%D", "ja-JP", UnixEpoch, "01/01/70")]
    [TestCase("%D", "ja-JP", SundayYearEnd, "12/31/23")]
    [TestCase("%D", "ja-JP", IsoWeekYearBoundary, "01/01/16")]
    [TestCase("%D", "es-ES", LeapDay, "02/29/00")]
    [TestCase("%D", "es-ES", UnixEpoch, "01/01/70")]
    [TestCase("%D", "es-ES", SundayYearEnd, "12/31/23")]
    [TestCase("%D", "es-ES", IsoWeekYearBoundary, "01/01/16")]
    [TestCase("%D", "ar-SA", LeapDay, "02/29/00")]
    [TestCase("%D", "ar-SA", UnixEpoch, "01/01/70")]
    [TestCase("%D", "ar-SA", SundayYearEnd, "12/31/23")]
    [TestCase("%D", "ar-SA", IsoWeekYearBoundary, "01/01/16")]
    public void TestStrftime_D(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%e", "C", LeapDay, "29")]
    [TestCase("%e", "C", UnixEpoch, " 1")]
    [TestCase("%e", "C", SundayYearEnd, "31")]
    [TestCase("%e", "C", IsoWeekYearBoundary, " 1")]
    [TestCase("%e", "en-GB", LeapDay, "29")]
    [TestCase("%e", "en-GB", UnixEpoch, " 1")]
    [TestCase("%e", "en-GB", SundayYearEnd, "31")]
    [TestCase("%e", "en-GB", IsoWeekYearBoundary, " 1")]
    [TestCase("%e", "ja-JP", LeapDay, "29")]
    [TestCase("%e", "ja-JP", UnixEpoch, " 1")]
    [TestCase("%e", "ja-JP", SundayYearEnd, "31")]
    [TestCase("%e", "ja-JP", IsoWeekYearBoundary, " 1")]
    [TestCase("%e", "es-ES", LeapDay, "29")]
    [TestCase("%e", "es-ES", UnixEpoch, " 1")]
    [TestCase("%e", "es-ES", SundayYearEnd, "31")]
    [TestCase("%e", "es-ES", IsoWeekYearBoundary, " 1")]
    [TestCase("%e", "ar-SA", LeapDay, "29")]
    [TestCase("%e", "ar-SA", UnixEpoch, " 1")]
    [TestCase("%e", "ar-SA", SundayYearEnd, "31")]
    [TestCase("%e", "ar-SA", IsoWeekYearBoundary, " 1")]
    public void TestStrftime_e(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%F", "C", LeapDay, "2000-02-29")]
    [TestCase("%F", "C", UnixEpoch, "1970-01-01")]
    [TestCase("%F", "C", SundayYearEnd, "2023-12-31")]
    [TestCase("%F", "C", IsoWeekYearBoundary, "2016-01-01")]
    [TestCase("%F", "en-GB", LeapDay, "2000-02-29")]
    [TestCase("%F", "en-GB", UnixEpoch, "1970-01-01")]
    [TestCase("%F", "en-GB", SundayYearEnd, "2023-12-31")]
    [TestCase("%F", "en-GB", IsoWeekYearBoundary, "2016-01-01")]
    [TestCase("%F", "ja-JP", LeapDay, "2000-02-29")]
    [TestCase("%F", "ja-JP", UnixEpoch, "1970-01-01")]
    [TestCase("%F", "ja-JP", SundayYearEnd, "2023-12-31")]
    [TestCase("%F", "ja-JP", IsoWeekYearBoundary, "2016-01-01")]
    [TestCase("%F", "es-ES", LeapDay, "2000-02-29")]
    [TestCase("%F", "es-ES", UnixEpoch, "1970-01-01")]
    [TestCase("%F", "es-ES", SundayYearEnd, "2023-12-31")]
    [TestCase("%F", "es-ES", IsoWeekYearBoundary, "2016-01-01")]
    [TestCase("%F", "ar-SA", LeapDay, "2000-02-29")]
    [TestCase("%F", "ar-SA", UnixEpoch, "1970-01-01")]
    [TestCase("%F", "ar-SA", SundayYearEnd, "2023-12-31")]
    [TestCase("%F", "ar-SA", IsoWeekYearBoundary, "2016-01-01")]
    public void TestStrftime_F(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%g", "C", LeapDay, "00")]
    [TestCase("%g", "C", UnixEpoch, "70")]
    [TestCase("%g", "C", SundayYearEnd, "23")]
    [TestCase("%g", "C", IsoWeekYearBoundary, "15")]
    [TestCase("%g", "en-GB", LeapDay, "00")]
    [TestCase("%g", "en-GB", UnixEpoch, "70")]
    [TestCase("%g", "en-GB", SundayYearEnd, "23")]
    [TestCase("%g", "en-GB", IsoWeekYearBoundary, "15")]
    [TestCase("%g", "ja-JP", LeapDay, "00")]
    [TestCase("%g", "ja-JP", UnixEpoch, "70")]
    [TestCase("%g", "ja-JP", SundayYearEnd, "23")]
    [TestCase("%g", "ja-JP", IsoWeekYearBoundary, "15")]
    [TestCase("%g", "es-ES", LeapDay, "00")]
    [TestCase("%g", "es-ES", UnixEpoch, "70")]
    [TestCase("%g", "es-ES", SundayYearEnd, "23")]
    [TestCase("%g", "es-ES", IsoWeekYearBoundary, "15")]
    [TestCase("%g", "ar-SA", LeapDay, "00")]
    [TestCase("%g", "ar-SA", UnixEpoch, "70")]
    [TestCase("%g", "ar-SA", SundayYearEnd, "23")]
    [TestCase("%g", "ar-SA", IsoWeekYearBoundary, "15")]
    public void TestStrftime_g(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%G", "C", LeapDay, "2000")]
    [TestCase("%G", "C", UnixEpoch, "1970")]
    [TestCase("%G", "C", SundayYearEnd, "2023")]
    [TestCase("%G", "C", IsoWeekYearBoundary, "2015")]
    [TestCase("%G", "en-GB", LeapDay, "2000")]
    [TestCase("%G", "en-GB", UnixEpoch, "1970")]
    [TestCase("%G", "en-GB", SundayYearEnd, "2023")]
    [TestCase("%G", "en-GB", IsoWeekYearBoundary, "2015")]
    [TestCase("%G", "ja-JP", LeapDay, "2000")]
    [TestCase("%G", "ja-JP", UnixEpoch, "1970")]
    [TestCase("%G", "ja-JP", SundayYearEnd, "2023")]
    [TestCase("%G", "ja-JP", IsoWeekYearBoundary, "2015")]
    [TestCase("%G", "es-ES", LeapDay, "2000")]
    [TestCase("%G", "es-ES", UnixEpoch, "1970")]
    [TestCase("%G", "es-ES", SundayYearEnd, "2023")]
    [TestCase("%G", "es-ES", IsoWeekYearBoundary, "2015")]
    [TestCase("%G", "ar-SA", LeapDay, "2000")]
    [TestCase("%G", "ar-SA", UnixEpoch, "1970")]
    [TestCase("%G", "ar-SA", SundayYearEnd, "2023")]
    [TestCase("%G", "ar-SA", IsoWeekYearBoundary, "2015")]
    public void TestStrftime_G(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%h", "C", LeapDay, "Feb")]
    [TestCase("%h", "C", UnixEpoch, "Jan")]
    [TestCase("%h", "C", SundayYearEnd, "Dec")]
    [TestCase("%h", "C", IsoWeekYearBoundary, "Jan")]
    [TestCase("%h", "en-GB", LeapDay, "Feb")]
    [TestCase("%h", "en-GB", UnixEpoch, "Jan")]
    [TestCase("%h", "en-GB", SundayYearEnd, "Dec")]
    [TestCase("%h", "en-GB", IsoWeekYearBoundary, "Jan")]
    [TestCase("%h", "ja-JP", LeapDay, "2月")]
    [TestCase("%h", "ja-JP", UnixEpoch, "1月")]
    [TestCase("%h", "ja-JP", SundayYearEnd, "12月")]
    [TestCase("%h", "ja-JP", IsoWeekYearBoundary, "1月")]
    [TestCase("%h", "es-ES", LeapDay, "feb")]
    [TestCase("%h", "es-ES", UnixEpoch, "ene")]
    [TestCase("%h", "es-ES", SundayYearEnd, "dic")]
    [TestCase("%h", "es-ES", IsoWeekYearBoundary, "ene")]
    [TestCase("%h", "ar-SA", LeapDay, "فبراير")]
    [TestCase("%h", "ar-SA", UnixEpoch, "يناير")]
    [TestCase("%h", "ar-SA", SundayYearEnd, "ديسمبر")]
    [TestCase("%h", "ar-SA", IsoWeekYearBoundary, "يناير")]
    public void TestStrftime_h(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%H", "C", LeapDay, "23")]
    [TestCase("%H", "C", UnixEpoch, "00")]
    [TestCase("%H", "C", SundayYearEnd, "13")]
    [TestCase("%H", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%H", "en-GB", LeapDay, "23")]
    [TestCase("%H", "en-GB", UnixEpoch, "00")]
    [TestCase("%H", "en-GB", SundayYearEnd, "13")]
    [TestCase("%H", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%H", "ja-JP", LeapDay, "23")]
    [TestCase("%H", "ja-JP", UnixEpoch, "00")]
    [TestCase("%H", "ja-JP", SundayYearEnd, "13")]
    [TestCase("%H", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%H", "es-ES", LeapDay, "23")]
    [TestCase("%H", "es-ES", UnixEpoch, "00")]
    [TestCase("%H", "es-ES", SundayYearEnd, "13")]
    [TestCase("%H", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%H", "ar-SA", LeapDay, "23")]
    [TestCase("%H", "ar-SA", UnixEpoch, "00")]
    [TestCase("%H", "ar-SA", SundayYearEnd, "13")]
    [TestCase("%H", "ar-SA", IsoWeekYearBoundary, "00")]
    public void TestStrftime_H(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%I", "C", LeapDay, "11")]
    [TestCase("%I", "C", UnixEpoch, "12")]
    [TestCase("%I", "C", SundayYearEnd, "01")]
    [TestCase("%I", "C", IsoWeekYearBoundary, "12")]
    [TestCase("%I", "en-GB", LeapDay, "11")]
    [TestCase("%I", "en-GB", UnixEpoch, "12")]
    [TestCase("%I", "en-GB", SundayYearEnd, "01")]
    [TestCase("%I", "en-GB", IsoWeekYearBoundary, "12")]
    [TestCase("%I", "ja-JP", LeapDay, "11")]
    [TestCase("%I", "ja-JP", UnixEpoch, "12")]
    [TestCase("%I", "ja-JP", SundayYearEnd, "01")]
    [TestCase("%I", "ja-JP", IsoWeekYearBoundary, "12")]
    [TestCase("%I", "es-ES", LeapDay, "11")]
    [TestCase("%I", "es-ES", UnixEpoch, "12")]
    [TestCase("%I", "es-ES", SundayYearEnd, "01")]
    [TestCase("%I", "es-ES", IsoWeekYearBoundary, "12")]
    [TestCase("%I", "ar-SA", LeapDay, "11")]
    [TestCase("%I", "ar-SA", UnixEpoch, "12")]
    [TestCase("%I", "ar-SA", SundayYearEnd, "01")]
    [TestCase("%I", "ar-SA", IsoWeekYearBoundary, "12")]
    public void TestStrftime_I(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%j", "C", LeapDay, "060")]
    [TestCase("%j", "C", UnixEpoch, "001")]
    [TestCase("%j", "C", SundayYearEnd, "365")]
    [TestCase("%j", "C", IsoWeekYearBoundary, "001")]
    [TestCase("%j", "en-GB", LeapDay, "060")]
    [TestCase("%j", "en-GB", UnixEpoch, "001")]
    [TestCase("%j", "en-GB", SundayYearEnd, "365")]
    [TestCase("%j", "en-GB", IsoWeekYearBoundary, "001")]
    [TestCase("%j", "ja-JP", LeapDay, "060")]
    [TestCase("%j", "ja-JP", UnixEpoch, "001")]
    [TestCase("%j", "ja-JP", SundayYearEnd, "365")]
    [TestCase("%j", "ja-JP", IsoWeekYearBoundary, "001")]
    [TestCase("%j", "es-ES", LeapDay, "060")]
    [TestCase("%j", "es-ES", UnixEpoch, "001")]
    [TestCase("%j", "es-ES", SundayYearEnd, "365")]
    [TestCase("%j", "es-ES", IsoWeekYearBoundary, "001")]
    [TestCase("%j", "ar-SA", LeapDay, "060")]
    [TestCase("%j", "ar-SA", UnixEpoch, "001")]
    [TestCase("%j", "ar-SA", SundayYearEnd, "365")]
    [TestCase("%j", "ar-SA", IsoWeekYearBoundary, "001")]
    public void TestStrftime_j(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%m", "C", LeapDay, "02")]
    [TestCase("%m", "C", UnixEpoch, "01")]
    [TestCase("%m", "C", SundayYearEnd, "12")]
    [TestCase("%m", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%m", "en-GB", LeapDay, "02")]
    [TestCase("%m", "en-GB", UnixEpoch, "01")]
    [TestCase("%m", "en-GB", SundayYearEnd, "12")]
    [TestCase("%m", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%m", "ja-JP", LeapDay, "02")]
    [TestCase("%m", "ja-JP", UnixEpoch, "01")]
    [TestCase("%m", "ja-JP", SundayYearEnd, "12")]
    [TestCase("%m", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%m", "es-ES", LeapDay, "02")]
    [TestCase("%m", "es-ES", UnixEpoch, "01")]
    [TestCase("%m", "es-ES", SundayYearEnd, "12")]
    [TestCase("%m", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%m", "ar-SA", LeapDay, "02")]
    [TestCase("%m", "ar-SA", UnixEpoch, "01")]
    [TestCase("%m", "ar-SA", SundayYearEnd, "12")]
    [TestCase("%m", "ar-SA", IsoWeekYearBoundary, "01")]
    public void TestStrftime_m(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%M", "C", LeapDay, "05")]
    [TestCase("%M", "C", UnixEpoch, "00")]
    [TestCase("%M", "C", SundayYearEnd, "14")]
    [TestCase("%M", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%M", "en-GB", LeapDay, "05")]
    [TestCase("%M", "en-GB", UnixEpoch, "00")]
    [TestCase("%M", "en-GB", SundayYearEnd, "14")]
    [TestCase("%M", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%M", "ja-JP", LeapDay, "05")]
    [TestCase("%M", "ja-JP", UnixEpoch, "00")]
    [TestCase("%M", "ja-JP", SundayYearEnd, "14")]
    [TestCase("%M", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%M", "es-ES", LeapDay, "05")]
    [TestCase("%M", "es-ES", UnixEpoch, "00")]
    [TestCase("%M", "es-ES", SundayYearEnd, "14")]
    [TestCase("%M", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%M", "ar-SA", LeapDay, "05")]
    [TestCase("%M", "ar-SA", UnixEpoch, "00")]
    [TestCase("%M", "ar-SA", SundayYearEnd, "14")]
    [TestCase("%M", "ar-SA", IsoWeekYearBoundary, "01")]
    public void TestStrftime_M(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%n", "C", LeapDay, "\n")]
    [TestCase("%n", "C", UnixEpoch, "\n")]
    [TestCase("%n", "C", SundayYearEnd, "\n")]
    [TestCase("%n", "C", IsoWeekYearBoundary, "\n")]
    [TestCase("%n", "en-GB", LeapDay, "\n")]
    [TestCase("%n", "en-GB", UnixEpoch, "\n")]
    [TestCase("%n", "en-GB", SundayYearEnd, "\n")]
    [TestCase("%n", "en-GB", IsoWeekYearBoundary, "\n")]
    [TestCase("%n", "ja-JP", LeapDay, "\n")]
    [TestCase("%n", "ja-JP", UnixEpoch, "\n")]
    [TestCase("%n", "ja-JP", SundayYearEnd, "\n")]
    [TestCase("%n", "ja-JP", IsoWeekYearBoundary, "\n")]
    [TestCase("%n", "es-ES", LeapDay, "\n")]
    [TestCase("%n", "es-ES", UnixEpoch, "\n")]
    [TestCase("%n", "es-ES", SundayYearEnd, "\n")]
    [TestCase("%n", "es-ES", IsoWeekYearBoundary, "\n")]
    [TestCase("%n", "ar-SA", LeapDay, "\n")]
    [TestCase("%n", "ar-SA", UnixEpoch, "\n")]
    [TestCase("%n", "ar-SA", SundayYearEnd, "\n")]
    [TestCase("%n", "ar-SA", IsoWeekYearBoundary, "\n")]
    public void TestStrftime_n(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%p", "C", LeapDay, "PM")]
    [TestCase("%p", "C", UnixEpoch, "AM")]
    [TestCase("%p", "C", SundayYearEnd, "PM")]
    [TestCase("%p", "C", IsoWeekYearBoundary, "AM")]
    [TestCase("%p", "en-GB", LeapDay, "pm")]
    [TestCase("%p", "en-GB", UnixEpoch, "am")]
    [TestCase("%p", "en-GB", SundayYearEnd, "pm")]
    [TestCase("%p", "en-GB", IsoWeekYearBoundary, "am")]
    [TestCase("%p", "ja-JP", LeapDay, "午後")]
    [TestCase("%p", "ja-JP", UnixEpoch, "午前")]
    [TestCase("%p", "ja-JP", SundayYearEnd, "午後")]
    [TestCase("%p", "ja-JP", IsoWeekYearBoundary, "午前")]
    [TestCase("%p", "es-ES", LeapDay, "p. m.")]
    [TestCase("%p", "es-ES", UnixEpoch, "a. m.")]
    [TestCase("%p", "es-ES", SundayYearEnd, "p. m.")]
    [TestCase("%p", "es-ES", IsoWeekYearBoundary, "a. m.")]
    [TestCase("%p", "ar-SA", LeapDay, "م")]
    [TestCase("%p", "ar-SA", UnixEpoch, "ص")]
    [TestCase("%p", "ar-SA", SundayYearEnd, "م")]
    [TestCase("%p", "ar-SA", IsoWeekYearBoundary, "ص")]
    public void TestStrftime_p(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%r", "C", LeapDay, "23:05:07")]
    [TestCase("%r", "C", UnixEpoch, "00:00:00")]
    [TestCase("%r", "C", SundayYearEnd, "13:14:15")]
    [TestCase("%r", "C", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%r", "en-GB", LeapDay, "23:05:07")]
    [TestCase("%r", "en-GB", UnixEpoch, "00:00:00")]
    [TestCase("%r", "en-GB", SundayYearEnd, "13:14:15")]
    [TestCase("%r", "en-GB", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%r", "ja-JP", LeapDay, "23:05:07")]
    [TestCase("%r", "ja-JP", UnixEpoch, "0:00:00")]
    [TestCase("%r", "ja-JP", SundayYearEnd, "13:14:15")]
    [TestCase("%r", "ja-JP", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%r", "es-ES", LeapDay, "23:05:07")]
    [TestCase("%r", "es-ES", UnixEpoch, "0:00:00")]
    [TestCase("%r", "es-ES", SundayYearEnd, "13:14:15")]
    [TestCase("%r", "es-ES", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%r", "ar-SA", LeapDay, "11:05:07 م")]
    [TestCase("%r", "ar-SA", UnixEpoch, "12:00:00 ص")]
    [TestCase("%r", "ar-SA", SundayYearEnd, "1:14:15 م")]
    [TestCase("%r", "ar-SA", IsoWeekYearBoundary, "12:01:02 ص")]
    public void TestStrftime_r(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%R", "C", LeapDay, "23:05")]
    [TestCase("%R", "C", UnixEpoch, "00:00")]
    [TestCase("%R", "C", SundayYearEnd, "13:14")]
    [TestCase("%R", "C", IsoWeekYearBoundary, "00:01")]
    [TestCase("%R", "en-GB", LeapDay, "23:05")]
    [TestCase("%R", "en-GB", UnixEpoch, "00:00")]
    [TestCase("%R", "en-GB", SundayYearEnd, "13:14")]
    [TestCase("%R", "en-GB", IsoWeekYearBoundary, "00:01")]
    [TestCase("%R", "ja-JP", LeapDay, "23:05")]
    [TestCase("%R", "ja-JP", UnixEpoch, "00:00")]
    [TestCase("%R", "ja-JP", SundayYearEnd, "13:14")]
    [TestCase("%R", "ja-JP", IsoWeekYearBoundary, "00:01")]
    [TestCase("%R", "es-ES", LeapDay, "23:05")]
    [TestCase("%R", "es-ES", UnixEpoch, "00:00")]
    [TestCase("%R", "es-ES", SundayYearEnd, "13:14")]
    [TestCase("%R", "es-ES", IsoWeekYearBoundary, "00:01")]
    [TestCase("%R", "ar-SA", LeapDay, "23:05")]
    [TestCase("%R", "ar-SA", UnixEpoch, "00:00")]
    [TestCase("%R", "ar-SA", SundayYearEnd, "13:14")]
    [TestCase("%R", "ar-SA", IsoWeekYearBoundary, "00:01")]
    public void TestStrftime_R(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%S", "C", LeapDay, "07")]
    [TestCase("%S", "C", UnixEpoch, "00")]
    [TestCase("%S", "C", SundayYearEnd, "15")]
    [TestCase("%S", "C", IsoWeekYearBoundary, "02")]
    [TestCase("%S", "en-GB", LeapDay, "07")]
    [TestCase("%S", "en-GB", UnixEpoch, "00")]
    [TestCase("%S", "en-GB", SundayYearEnd, "15")]
    [TestCase("%S", "en-GB", IsoWeekYearBoundary, "02")]
    [TestCase("%S", "ja-JP", LeapDay, "07")]
    [TestCase("%S", "ja-JP", UnixEpoch, "00")]
    [TestCase("%S", "ja-JP", SundayYearEnd, "15")]
    [TestCase("%S", "ja-JP", IsoWeekYearBoundary, "02")]
    [TestCase("%S", "es-ES", LeapDay, "07")]
    [TestCase("%S", "es-ES", UnixEpoch, "00")]
    [TestCase("%S", "es-ES", SundayYearEnd, "15")]
    [TestCase("%S", "es-ES", IsoWeekYearBoundary, "02")]
    [TestCase("%S", "ar-SA", LeapDay, "07")]
    [TestCase("%S", "ar-SA", UnixEpoch, "00")]
    [TestCase("%S", "ar-SA", SundayYearEnd, "15")]
    [TestCase("%S", "ar-SA", IsoWeekYearBoundary, "02")]
    public void TestStrftime_S(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%t", "C", LeapDay, "\t")]
    [TestCase("%t", "C", UnixEpoch, "\t")]
    [TestCase("%t", "C", SundayYearEnd, "\t")]
    [TestCase("%t", "C", IsoWeekYearBoundary, "\t")]
    [TestCase("%t", "en-GB", LeapDay, "\t")]
    [TestCase("%t", "en-GB", UnixEpoch, "\t")]
    [TestCase("%t", "en-GB", SundayYearEnd, "\t")]
    [TestCase("%t", "en-GB", IsoWeekYearBoundary, "\t")]
    [TestCase("%t", "ja-JP", LeapDay, "\t")]
    [TestCase("%t", "ja-JP", UnixEpoch, "\t")]
    [TestCase("%t", "ja-JP", SundayYearEnd, "\t")]
    [TestCase("%t", "ja-JP", IsoWeekYearBoundary, "\t")]
    [TestCase("%t", "es-ES", LeapDay, "\t")]
    [TestCase("%t", "es-ES", UnixEpoch, "\t")]
    [TestCase("%t", "es-ES", SundayYearEnd, "\t")]
    [TestCase("%t", "es-ES", IsoWeekYearBoundary, "\t")]
    [TestCase("%t", "ar-SA", LeapDay, "\t")]
    [TestCase("%t", "ar-SA", UnixEpoch, "\t")]
    [TestCase("%t", "ar-SA", SundayYearEnd, "\t")]
    [TestCase("%t", "ar-SA", IsoWeekYearBoundary, "\t")]
    public void TestStrftime_t(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%T", "C", LeapDay, "23:05:07")]
    [TestCase("%T", "C", UnixEpoch, "00:00:00")]
    [TestCase("%T", "C", SundayYearEnd, "13:14:15")]
    [TestCase("%T", "C", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%T", "en-GB", LeapDay, "23:05:07")]
    [TestCase("%T", "en-GB", UnixEpoch, "00:00:00")]
    [TestCase("%T", "en-GB", SundayYearEnd, "13:14:15")]
    [TestCase("%T", "en-GB", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%T", "ja-JP", LeapDay, "23:05:07")]
    [TestCase("%T", "ja-JP", UnixEpoch, "00:00:00")]
    [TestCase("%T", "ja-JP", SundayYearEnd, "13:14:15")]
    [TestCase("%T", "ja-JP", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%T", "es-ES", LeapDay, "23:05:07")]
    [TestCase("%T", "es-ES", UnixEpoch, "00:00:00")]
    [TestCase("%T", "es-ES", SundayYearEnd, "13:14:15")]
    [TestCase("%T", "es-ES", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%T", "ar-SA", LeapDay, "23:05:07")]
    [TestCase("%T", "ar-SA", UnixEpoch, "00:00:00")]
    [TestCase("%T", "ar-SA", SundayYearEnd, "13:14:15")]
    [TestCase("%T", "ar-SA", IsoWeekYearBoundary, "00:01:02")]
    public void TestStrftime_T(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%u", "C", LeapDay, "2")]
    [TestCase("%u", "C", UnixEpoch, "4")]
    [TestCase("%u", "C", SundayYearEnd, "7")]
    [TestCase("%u", "C", IsoWeekYearBoundary, "5")]
    [TestCase("%u", "en-GB", LeapDay, "2")]
    [TestCase("%u", "en-GB", UnixEpoch, "4")]
    [TestCase("%u", "en-GB", SundayYearEnd, "7")]
    [TestCase("%u", "en-GB", IsoWeekYearBoundary, "5")]
    [TestCase("%u", "ja-JP", LeapDay, "2")]
    [TestCase("%u", "ja-JP", UnixEpoch, "4")]
    [TestCase("%u", "ja-JP", SundayYearEnd, "7")]
    [TestCase("%u", "ja-JP", IsoWeekYearBoundary, "5")]
    [TestCase("%u", "es-ES", LeapDay, "2")]
    [TestCase("%u", "es-ES", UnixEpoch, "4")]
    [TestCase("%u", "es-ES", SundayYearEnd, "7")]
    [TestCase("%u", "es-ES", IsoWeekYearBoundary, "5")]
    [TestCase("%u", "ar-SA", LeapDay, "2")]
    [TestCase("%u", "ar-SA", UnixEpoch, "4")]
    [TestCase("%u", "ar-SA", SundayYearEnd, "7")]
    [TestCase("%u", "ar-SA", IsoWeekYearBoundary, "5")]
    public void TestStrftime_u(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%U", "C", LeapDay, "09")]
    [TestCase("%U", "C", UnixEpoch, "00")]
    [TestCase("%U", "C", SundayYearEnd, "53")]
    [TestCase("%U", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%U", "en-GB", LeapDay, "09")]
    [TestCase("%U", "en-GB", UnixEpoch, "00")]
    [TestCase("%U", "en-GB", SundayYearEnd, "53")]
    [TestCase("%U", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%U", "ja-JP", LeapDay, "09")]
    [TestCase("%U", "ja-JP", UnixEpoch, "00")]
    [TestCase("%U", "ja-JP", SundayYearEnd, "53")]
    [TestCase("%U", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%U", "es-ES", LeapDay, "09")]
    [TestCase("%U", "es-ES", UnixEpoch, "00")]
    [TestCase("%U", "es-ES", SundayYearEnd, "53")]
    [TestCase("%U", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%U", "ar-SA", LeapDay, "09")]
    [TestCase("%U", "ar-SA", UnixEpoch, "00")]
    [TestCase("%U", "ar-SA", SundayYearEnd, "53")]
    [TestCase("%U", "ar-SA", IsoWeekYearBoundary, "00")]
    public void TestStrftime_U(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%V", "C", LeapDay, "09")]
    [TestCase("%V", "C", UnixEpoch, "01")]
    [TestCase("%V", "C", SundayYearEnd, "52")]
    [TestCase("%V", "C", IsoWeekYearBoundary, "53")]
    [TestCase("%V", "en-GB", LeapDay, "09")]
    [TestCase("%V", "en-GB", UnixEpoch, "01")]
    [TestCase("%V", "en-GB", SundayYearEnd, "52")]
    [TestCase("%V", "en-GB", IsoWeekYearBoundary, "53")]
    [TestCase("%V", "ja-JP", LeapDay, "09")]
    [TestCase("%V", "ja-JP", UnixEpoch, "01")]
    [TestCase("%V", "ja-JP", SundayYearEnd, "52")]
    [TestCase("%V", "ja-JP", IsoWeekYearBoundary, "53")]
    [TestCase("%V", "es-ES", LeapDay, "09")]
    [TestCase("%V", "es-ES", UnixEpoch, "01")]
    [TestCase("%V", "es-ES", SundayYearEnd, "52")]
    [TestCase("%V", "es-ES", IsoWeekYearBoundary, "53")]
    [TestCase("%V", "ar-SA", LeapDay, "09")]
    [TestCase("%V", "ar-SA", UnixEpoch, "01")]
    [TestCase("%V", "ar-SA", SundayYearEnd, "52")]
    [TestCase("%V", "ar-SA", IsoWeekYearBoundary, "53")]
    public void TestStrftime_V(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%w", "C", LeapDay, "2")]
    [TestCase("%w", "C", UnixEpoch, "4")]
    [TestCase("%w", "C", SundayYearEnd, "0")]
    [TestCase("%w", "C", IsoWeekYearBoundary, "5")]
    [TestCase("%w", "en-GB", LeapDay, "2")]
    [TestCase("%w", "en-GB", UnixEpoch, "4")]
    [TestCase("%w", "en-GB", SundayYearEnd, "0")]
    [TestCase("%w", "en-GB", IsoWeekYearBoundary, "5")]
    [TestCase("%w", "ja-JP", LeapDay, "2")]
    [TestCase("%w", "ja-JP", UnixEpoch, "4")]
    [TestCase("%w", "ja-JP", SundayYearEnd, "0")]
    [TestCase("%w", "ja-JP", IsoWeekYearBoundary, "5")]
    [TestCase("%w", "es-ES", LeapDay, "2")]
    [TestCase("%w", "es-ES", UnixEpoch, "4")]
    [TestCase("%w", "es-ES", SundayYearEnd, "0")]
    [TestCase("%w", "es-ES", IsoWeekYearBoundary, "5")]
    [TestCase("%w", "ar-SA", LeapDay, "2")]
    [TestCase("%w", "ar-SA", UnixEpoch, "4")]
    [TestCase("%w", "ar-SA", SundayYearEnd, "0")]
    [TestCase("%w", "ar-SA", IsoWeekYearBoundary, "5")]
    public void TestStrftime_w(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%W", "C", LeapDay, "09")]
    [TestCase("%W", "C", UnixEpoch, "00")]
    [TestCase("%W", "C", SundayYearEnd, "52")]
    [TestCase("%W", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%W", "en-GB", LeapDay, "09")]
    [TestCase("%W", "en-GB", UnixEpoch, "00")]
    [TestCase("%W", "en-GB", SundayYearEnd, "52")]
    [TestCase("%W", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%W", "ja-JP", LeapDay, "09")]
    [TestCase("%W", "ja-JP", UnixEpoch, "00")]
    [TestCase("%W", "ja-JP", SundayYearEnd, "52")]
    [TestCase("%W", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%W", "es-ES", LeapDay, "09")]
    [TestCase("%W", "es-ES", UnixEpoch, "00")]
    [TestCase("%W", "es-ES", SundayYearEnd, "52")]
    [TestCase("%W", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%W", "ar-SA", LeapDay, "09")]
    [TestCase("%W", "ar-SA", UnixEpoch, "00")]
    [TestCase("%W", "ar-SA", SundayYearEnd, "52")]
    [TestCase("%W", "ar-SA", IsoWeekYearBoundary, "00")]
    public void TestStrftime_W(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%x", "C", LeapDay, "02/29/2000")]
    [TestCase("%x", "C", UnixEpoch, "01/01/1970")]
    [TestCase("%x", "C", SundayYearEnd, "12/31/2023")]
    [TestCase("%x", "C", IsoWeekYearBoundary, "01/01/2016")]
    [TestCase("%x", "en-GB", LeapDay, "29/02/2000")]
    [TestCase("%x", "en-GB", UnixEpoch, "01/01/1970")]
    [TestCase("%x", "en-GB", SundayYearEnd, "31/12/2023")]
    [TestCase("%x", "en-GB", IsoWeekYearBoundary, "01/01/2016")]
    [TestCase("%x", "ja-JP", LeapDay, "2000/02/29")]
    [TestCase("%x", "ja-JP", UnixEpoch, "1970/01/01")]
    [TestCase("%x", "ja-JP", SundayYearEnd, "2023/12/31")]
    [TestCase("%x", "ja-JP", IsoWeekYearBoundary, "2016/01/01")]
    [TestCase("%x", "es-ES", LeapDay, "29/2/2000")]
    [TestCase("%x", "es-ES", UnixEpoch, "1/1/1970")]
    [TestCase("%x", "es-ES", SundayYearEnd, "31/12/2023")]
    [TestCase("%x", "es-ES", IsoWeekYearBoundary, "1/1/2016")]
    [TestCase("%x", "ar-SA", LeapDay, "29‏‏/2‏‏/2000 م")]
    [TestCase("%x", "ar-SA", UnixEpoch, "1‏‏/1‏‏/1970 م")]
    [TestCase("%x", "ar-SA", SundayYearEnd, "31‏‏/12‏‏/2023 م")]
    [TestCase("%x", "ar-SA", IsoWeekYearBoundary, "1‏‏/1‏‏/2016 م")]
    public void TestStrftime_x(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%X", "C", LeapDay, "23:05:07")]
    [TestCase("%X", "C", UnixEpoch, "00:00:00")]
    [TestCase("%X", "C", SundayYearEnd, "13:14:15")]
    [TestCase("%X", "C", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%X", "en-GB", LeapDay, "23:05:07")]
    [TestCase("%X", "en-GB", UnixEpoch, "00:00:00")]
    [TestCase("%X", "en-GB", SundayYearEnd, "13:14:15")]
    [TestCase("%X", "en-GB", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%X", "ja-JP", LeapDay, "23:05:07")]
    [TestCase("%X", "ja-JP", UnixEpoch, "0:00:00")]
    [TestCase("%X", "ja-JP", SundayYearEnd, "13:14:15")]
    [TestCase("%X", "ja-JP", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%X", "es-ES", LeapDay, "23:05:07")]
    [TestCase("%X", "es-ES", UnixEpoch, "0:00:00")]
    [TestCase("%X", "es-ES", SundayYearEnd, "13:14:15")]
    [TestCase("%X", "es-ES", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%X", "ar-SA", LeapDay, "11:05:07 م")]
    [TestCase("%X", "ar-SA", UnixEpoch, "12:00:00 ص")]
    [TestCase("%X", "ar-SA", SundayYearEnd, "1:14:15 م")]
    [TestCase("%X", "ar-SA", IsoWeekYearBoundary, "12:01:02 ص")]
    public void TestStrftime_X(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%y", "C", LeapDay, "00")]
    [TestCase("%y", "C", UnixEpoch, "70")]
    [TestCase("%y", "C", SundayYearEnd, "23")]
    [TestCase("%y", "C", IsoWeekYearBoundary, "16")]
    [TestCase("%y", "en-GB", LeapDay, "00")]
    [TestCase("%y", "en-GB", UnixEpoch, "70")]
    [TestCase("%y", "en-GB", SundayYearEnd, "23")]
    [TestCase("%y", "en-GB", IsoWeekYearBoundary, "16")]
    [TestCase("%y", "ja-JP", LeapDay, "00")]
    [TestCase("%y", "ja-JP", UnixEpoch, "70")]
    [TestCase("%y", "ja-JP", SundayYearEnd, "23")]
    [TestCase("%y", "ja-JP", IsoWeekYearBoundary, "16")]
    [TestCase("%y", "es-ES", LeapDay, "00")]
    [TestCase("%y", "es-ES", UnixEpoch, "70")]
    [TestCase("%y", "es-ES", SundayYearEnd, "23")]
    [TestCase("%y", "es-ES", IsoWeekYearBoundary, "16")]
    [TestCase("%y", "ar-SA", LeapDay, "00")]
    [TestCase("%y", "ar-SA", UnixEpoch, "70")]
    [TestCase("%y", "ar-SA", SundayYearEnd, "23")]
    [TestCase("%y", "ar-SA", IsoWeekYearBoundary, "16")]
    public void TestStrftime_y(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Y", "C", LeapDay, "2000")]
    [TestCase("%Y", "C", UnixEpoch, "1970")]
    [TestCase("%Y", "C", SundayYearEnd, "2023")]
    [TestCase("%Y", "C", IsoWeekYearBoundary, "2016")]
    [TestCase("%Y", "en-GB", LeapDay, "2000")]
    [TestCase("%Y", "en-GB", UnixEpoch, "1970")]
    [TestCase("%Y", "en-GB", SundayYearEnd, "2023")]
    [TestCase("%Y", "en-GB", IsoWeekYearBoundary, "2016")]
    [TestCase("%Y", "ja-JP", LeapDay, "2000")]
    [TestCase("%Y", "ja-JP", UnixEpoch, "1970")]
    [TestCase("%Y", "ja-JP", SundayYearEnd, "2023")]
    [TestCase("%Y", "ja-JP", IsoWeekYearBoundary, "2016")]
    [TestCase("%Y", "es-ES", LeapDay, "2000")]
    [TestCase("%Y", "es-ES", UnixEpoch, "1970")]
    [TestCase("%Y", "es-ES", SundayYearEnd, "2023")]
    [TestCase("%Y", "es-ES", IsoWeekYearBoundary, "2016")]
    [TestCase("%Y", "ar-SA", LeapDay, "2000")]
    [TestCase("%Y", "ar-SA", UnixEpoch, "1970")]
    [TestCase("%Y", "ar-SA", SundayYearEnd, "2023")]
    [TestCase("%Y", "ar-SA", IsoWeekYearBoundary, "2016")]
    public void TestStrftime_Y(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%z", "C", LeapDay, "+0000")]
    [TestCase("%z", "C", UnixEpoch, "+0000")]
    [TestCase("%z", "C", SundayYearEnd, "+0000")]
    [TestCase("%z", "C", IsoWeekYearBoundary, "+0000")]
    [TestCase("%z", "en-GB", LeapDay, "+0000")]
    [TestCase("%z", "en-GB", UnixEpoch, "+0000")]
    [TestCase("%z", "en-GB", SundayYearEnd, "+0000")]
    [TestCase("%z", "en-GB", IsoWeekYearBoundary, "+0000")]
    [TestCase("%z", "ja-JP", LeapDay, "+0000")]
    [TestCase("%z", "ja-JP", UnixEpoch, "+0000")]
    [TestCase("%z", "ja-JP", SundayYearEnd, "+0000")]
    [TestCase("%z", "ja-JP", IsoWeekYearBoundary, "+0000")]
    [TestCase("%z", "es-ES", LeapDay, "+0000")]
    [TestCase("%z", "es-ES", UnixEpoch, "+0000")]
    [TestCase("%z", "es-ES", SundayYearEnd, "+0000")]
    [TestCase("%z", "es-ES", IsoWeekYearBoundary, "+0000")]
    [TestCase("%z", "ar-SA", LeapDay, "+0000")]
    [TestCase("%z", "ar-SA", UnixEpoch, "+0000")]
    [TestCase("%z", "ar-SA", SundayYearEnd, "+0000")]
    [TestCase("%z", "ar-SA", IsoWeekYearBoundary, "+0000")]
    public void TestStrftime_z(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Z", "C", LeapDay, "UTC")]
    [TestCase("%Z", "C", UnixEpoch, "UTC")]
    [TestCase("%Z", "C", SundayYearEnd, "UTC")]
    [TestCase("%Z", "C", IsoWeekYearBoundary, "UTC")]
    [TestCase("%Z", "en-GB", LeapDay, "UTC")]
    [TestCase("%Z", "en-GB", UnixEpoch, "UTC")]
    [TestCase("%Z", "en-GB", SundayYearEnd, "UTC")]
    [TestCase("%Z", "en-GB", IsoWeekYearBoundary, "UTC")]
    [TestCase("%Z", "ja-JP", LeapDay, "UTC")]
    [TestCase("%Z", "ja-JP", UnixEpoch, "UTC")]
    [TestCase("%Z", "ja-JP", SundayYearEnd, "UTC")]
    [TestCase("%Z", "ja-JP", IsoWeekYearBoundary, "UTC")]
    [TestCase("%Z", "es-ES", LeapDay, "UTC")]
    [TestCase("%Z", "es-ES", UnixEpoch, "UTC")]
    [TestCase("%Z", "es-ES", SundayYearEnd, "UTC")]
    [TestCase("%Z", "es-ES", IsoWeekYearBoundary, "UTC")]
    [TestCase("%Z", "ar-SA", LeapDay, "UTC")]
    [TestCase("%Z", "ar-SA", UnixEpoch, "UTC")]
    [TestCase("%Z", "ar-SA", SundayYearEnd, "UTC")]
    [TestCase("%Z", "ar-SA", IsoWeekYearBoundary, "UTC")]
    public void TestStrftime_Z(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%%", "C", LeapDay, "%")]
    [TestCase("%%", "C", UnixEpoch, "%")]
    [TestCase("%%", "C", SundayYearEnd, "%")]
    [TestCase("%%", "C", IsoWeekYearBoundary, "%")]
    [TestCase("%%", "en-GB", LeapDay, "%")]
    [TestCase("%%", "en-GB", UnixEpoch, "%")]
    [TestCase("%%", "en-GB", SundayYearEnd, "%")]
    [TestCase("%%", "en-GB", IsoWeekYearBoundary, "%")]
    [TestCase("%%", "ja-JP", LeapDay, "%")]
    [TestCase("%%", "ja-JP", UnixEpoch, "%")]
    [TestCase("%%", "ja-JP", SundayYearEnd, "%")]
    [TestCase("%%", "ja-JP", IsoWeekYearBoundary, "%")]
    [TestCase("%%", "es-ES", LeapDay, "%")]
    [TestCase("%%", "es-ES", UnixEpoch, "%")]
    [TestCase("%%", "es-ES", SundayYearEnd, "%")]
    [TestCase("%%", "es-ES", IsoWeekYearBoundary, "%")]
    [TestCase("%%", "ar-SA", LeapDay, "%")]
    [TestCase("%%", "ar-SA", UnixEpoch, "%")]
    [TestCase("%%", "ar-SA", SundayYearEnd, "%")]
    [TestCase("%%", "ar-SA", IsoWeekYearBoundary, "%")]
    public void TestStrftime_AMP(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Ec", "C", LeapDay, "Tuesday, 29 February 2000 23:05:07")]
    [TestCase("%Ec", "C", UnixEpoch, "Thursday, 01 January 1970 00:00:00")]
    [TestCase("%Ec", "C", SundayYearEnd, "Sunday, 31 December 2023 13:14:15")]
    [TestCase("%Ec", "C", IsoWeekYearBoundary, "Friday, 01 January 2016 00:01:02")]
    [TestCase("%Ec", "en-GB", LeapDay, "Tuesday, 29 February 2000 23:05:07")]
    [TestCase("%Ec", "en-GB", UnixEpoch, "Thursday, 1 January 1970 00:00:00")]
    [TestCase("%Ec", "en-GB", SundayYearEnd, "Sunday, 31 December 2023 13:14:15")]
    [TestCase("%Ec", "en-GB", IsoWeekYearBoundary, "Friday, 1 January 2016 00:01:02")]
    [TestCase("%Ec", "ja-JP", LeapDay, "12年2月29日火曜日 23:05:07")]
    [TestCase("%Ec", "ja-JP", UnixEpoch, "45年1月1日木曜日 0:00:00")]
    [TestCase("%Ec", "ja-JP", SundayYearEnd, "05年12月31日日曜日 13:14:15")]
    [TestCase("%Ec", "ja-JP", IsoWeekYearBoundary, "28年1月1日金曜日 0:01:02")]
    [TestCase("%Ec", "es-ES", LeapDay, "martes, 29 de febrero de 2000 23:05:07")]
    [TestCase("%Ec", "es-ES", UnixEpoch, "jueves, 1 de enero de 1970 0:00:00")]
    [TestCase("%Ec", "es-ES", SundayYearEnd, "domingo, 31 de diciembre de 2023 13:14:15")]
    [TestCase("%Ec", "es-ES", IsoWeekYearBoundary, "viernes, 1 de enero de 2016 0:01:02")]
    [TestCase("%Ec", "ar-SA", LeapDay, "الثلاثاء، 23 ذو القعدة 1420 بعد الهجرة 11:05:07 م")]
    [TestCase("%Ec", "ar-SA", UnixEpoch, "الخميس، 23 شوال 1389 بعد الهجرة 12:00:00 ص")]
    [TestCase("%Ec", "ar-SA", SundayYearEnd, "الأحد، 18 جمادى الآخرة 1445 بعد الهجرة 1:14:15 م")]
    [TestCase("%Ec", "ar-SA", IsoWeekYearBoundary, "الجمعة، 21 ربيع الأول 1437 بعد الهجرة 12:01:02 ص")]
    public void TestStrftime_Ec(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }
    
    [TestCase("%EC", "C", LeapDay, "A.D.")]
    [TestCase("%EC", "C", UnixEpoch, "A.D.")]
    [TestCase("%EC", "C", SundayYearEnd, "A.D.")]
    [TestCase("%EC", "C", IsoWeekYearBoundary, "A.D.")]
    [TestCase("%EC", "en-GB", LeapDay, "AD")]
    [TestCase("%EC", "en-GB", UnixEpoch, "AD")]
    [TestCase("%EC", "en-GB", SundayYearEnd, "AD")]
    [TestCase("%EC", "en-GB", IsoWeekYearBoundary, "AD")]
    [TestCase("%EC", "ja-JP", LeapDay, "平成")]
    [TestCase("%EC", "ja-JP", UnixEpoch, "昭和")]
    [TestCase("%EC", "ja-JP", SundayYearEnd, "令和")]
    [TestCase("%EC", "ja-JP", IsoWeekYearBoundary, "平成")]
    [TestCase("%EC", "es-ES", LeapDay, "d. C.")]
    [TestCase("%EC", "es-ES", UnixEpoch, "d. C.")]
    [TestCase("%EC", "es-ES", SundayYearEnd, "d. C.")]
    [TestCase("%EC", "es-ES", IsoWeekYearBoundary, "d. C.")]
    [TestCase("%EC", "ar-SA", LeapDay, "بعد الهجرة")]
    [TestCase("%EC", "ar-SA", UnixEpoch, "بعد الهجرة")]
    [TestCase("%EC", "ar-SA", SundayYearEnd, "بعد الهجرة")]
    [TestCase("%EC", "ar-SA", IsoWeekYearBoundary, "بعد الهجرة")]
    public void TestStrftime_EC(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Ex", "C", LeapDay, "02/29/2000")]
    [TestCase("%Ex", "C", UnixEpoch, "01/01/1970")]
    [TestCase("%Ex", "C", SundayYearEnd, "12/31/2023")]
    [TestCase("%Ex", "C", IsoWeekYearBoundary, "01/01/2016")]
    [TestCase("%Ex", "en-GB", LeapDay, "29/02/2000")]
    [TestCase("%Ex", "en-GB", UnixEpoch, "01/01/1970")]
    [TestCase("%Ex", "en-GB", SundayYearEnd, "31/12/2023")]
    [TestCase("%Ex", "en-GB", IsoWeekYearBoundary, "01/01/2016")]
    [TestCase("%Ex", "ja-JP", LeapDay, "12/02/29")]
    [TestCase("%Ex", "ja-JP", UnixEpoch, "45/01/01")]
    [TestCase("%Ex", "ja-JP", SundayYearEnd, "05/12/31")]
    [TestCase("%Ex", "ja-JP", IsoWeekYearBoundary, "28/01/01")]
    [TestCase("%Ex", "es-ES", LeapDay, "29/2/2000")]
    [TestCase("%Ex", "es-ES", UnixEpoch, "1/1/1970")]
    [TestCase("%Ex", "es-ES", SundayYearEnd, "31/12/2023")]
    [TestCase("%Ex", "es-ES", IsoWeekYearBoundary, "1/1/2016")]
    [TestCase("%Ex", "ar-SA", LeapDay, "23‏‏/11‏‏/1420 بعد الهجرة")]
    [TestCase("%Ex", "ar-SA", UnixEpoch, "23‏‏/10‏‏/1389 بعد الهجرة")]
    [TestCase("%Ex", "ar-SA", SundayYearEnd, "18‏‏/6‏‏/1445 بعد الهجرة")]
    [TestCase("%Ex", "ar-SA", IsoWeekYearBoundary, "21‏‏/3‏‏/1437 بعد الهجرة")]
    public void TestStrftime_Ex(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%EX", "C", LeapDay, "23:05:07")]
    [TestCase("%EX", "C", UnixEpoch, "00:00:00")]
    [TestCase("%EX", "C", SundayYearEnd, "13:14:15")]
    [TestCase("%EX", "C", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%EX", "en-GB", LeapDay, "23:05:07")]
    [TestCase("%EX", "en-GB", UnixEpoch, "00:00:00")]
    [TestCase("%EX", "en-GB", SundayYearEnd, "13:14:15")]
    [TestCase("%EX", "en-GB", IsoWeekYearBoundary, "00:01:02")]
    [TestCase("%EX", "ja-JP", LeapDay, "23:05:07")]
    [TestCase("%EX", "ja-JP", UnixEpoch, "0:00:00")]
    [TestCase("%EX", "ja-JP", SundayYearEnd, "13:14:15")]
    [TestCase("%EX", "ja-JP", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%EX", "es-ES", LeapDay, "23:05:07")]
    [TestCase("%EX", "es-ES", UnixEpoch, "0:00:00")]
    [TestCase("%EX", "es-ES", SundayYearEnd, "13:14:15")]
    [TestCase("%EX", "es-ES", IsoWeekYearBoundary, "0:01:02")]
    [TestCase("%EX", "ar-SA", LeapDay, "11:05:07 م")]
    [TestCase("%EX", "ar-SA", UnixEpoch, "12:00:00 ص")]
    [TestCase("%EX", "ar-SA", SundayYearEnd, "1:14:15 م")]
    [TestCase("%EX", "ar-SA", IsoWeekYearBoundary, "12:01:02 ص")]
    public void TestStrftime_EX(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Ey", "C", LeapDay, "00")]
    [TestCase("%Ey", "C", UnixEpoch, "70")]
    [TestCase("%Ey", "C", SundayYearEnd, "23")]
    [TestCase("%Ey", "C", IsoWeekYearBoundary, "16")]
    [TestCase("%Ey", "en-GB", LeapDay, "00")]
    [TestCase("%Ey", "en-GB", UnixEpoch, "70")]
    [TestCase("%Ey", "en-GB", SundayYearEnd, "23")]
    [TestCase("%Ey", "en-GB", IsoWeekYearBoundary, "16")]
    [TestCase("%Ey", "ja-JP", LeapDay, "12")]
    [TestCase("%Ey", "ja-JP", UnixEpoch, "45")]
    [TestCase("%Ey", "ja-JP", SundayYearEnd, "05")]
    [TestCase("%Ey", "ja-JP", IsoWeekYearBoundary, "28")]
    [TestCase("%Ey", "es-ES", LeapDay, "00")]
    [TestCase("%Ey", "es-ES", UnixEpoch, "70")]
    [TestCase("%Ey", "es-ES", SundayYearEnd, "23")]
    [TestCase("%Ey", "es-ES", IsoWeekYearBoundary, "16")]
    [TestCase("%Ey", "ar-SA", LeapDay, "20")]
    [TestCase("%Ey", "ar-SA", UnixEpoch, "89")]
    [TestCase("%Ey", "ar-SA", SundayYearEnd, "45")]
    [TestCase("%Ey", "ar-SA", IsoWeekYearBoundary, "37")]
    public void TestStrftime_Ey(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%EY", "C", LeapDay, "2000")]
    [TestCase("%EY", "C", UnixEpoch, "1970")]
    [TestCase("%EY", "C", SundayYearEnd, "2023")]
    [TestCase("%EY", "C", IsoWeekYearBoundary, "2016")]
    [TestCase("%EY", "en-GB", LeapDay, "2000")]
    [TestCase("%EY", "en-GB", UnixEpoch, "1970")]
    [TestCase("%EY", "en-GB", SundayYearEnd, "2023")]
    [TestCase("%EY", "en-GB", IsoWeekYearBoundary, "2016")]
    [TestCase("%EY", "ja-JP", LeapDay, "12")]
    [TestCase("%EY", "ja-JP", UnixEpoch, "45")]
    [TestCase("%EY", "ja-JP", SundayYearEnd, "05")]
    [TestCase("%EY", "ja-JP", IsoWeekYearBoundary, "28")]
    [TestCase("%EY", "es-ES", LeapDay, "2000")]
    [TestCase("%EY", "es-ES", UnixEpoch, "1970")]
    [TestCase("%EY", "es-ES", SundayYearEnd, "2023")]
    [TestCase("%EY", "es-ES", IsoWeekYearBoundary, "2016")]
    [TestCase("%EY", "ar-SA", LeapDay, "1420")]
    [TestCase("%EY", "ar-SA", UnixEpoch, "1389")]
    [TestCase("%EY", "ar-SA", SundayYearEnd, "1445")]
    [TestCase("%EY", "ar-SA", IsoWeekYearBoundary, "1437")]
    public void TestStrftime_EY(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Od", "C", LeapDay, "29")]
    [TestCase("%Od", "C", UnixEpoch, "01")]
    [TestCase("%Od", "C", SundayYearEnd, "31")]
    [TestCase("%Od", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%Od", "en-GB", LeapDay, "29")]
    [TestCase("%Od", "en-GB", UnixEpoch, "01")]
    [TestCase("%Od", "en-GB", SundayYearEnd, "31")]
    [TestCase("%Od", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%Od", "ja-JP", LeapDay, "29")]
    [TestCase("%Od", "ja-JP", UnixEpoch, "01")]
    [TestCase("%Od", "ja-JP", SundayYearEnd, "31")]
    [TestCase("%Od", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%Od", "es-ES", LeapDay, "29")]
    [TestCase("%Od", "es-ES", UnixEpoch, "01")]
    [TestCase("%Od", "es-ES", SundayYearEnd, "31")]
    [TestCase("%Od", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%Od", "ar-SA", LeapDay, "٢٩")]
    [TestCase("%Od", "ar-SA", UnixEpoch, "٠١")]
    [TestCase("%Od", "ar-SA", SundayYearEnd, "٣١")]
    [TestCase("%Od", "ar-SA", IsoWeekYearBoundary, "٠١")]
    public void TestStrftime_Od(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Oe", "C", LeapDay, "29")]
    [TestCase("%Oe", "C", UnixEpoch, " 1")]
    [TestCase("%Oe", "C", SundayYearEnd, "31")]
    [TestCase("%Oe", "C", IsoWeekYearBoundary, " 1")]
    [TestCase("%Oe", "en-GB", LeapDay, "29")]
    [TestCase("%Oe", "en-GB", UnixEpoch, " 1")]
    [TestCase("%Oe", "en-GB", SundayYearEnd, "31")]
    [TestCase("%Oe", "en-GB", IsoWeekYearBoundary, " 1")]
    [TestCase("%Oe", "ja-JP", LeapDay, "29")]
    [TestCase("%Oe", "ja-JP", UnixEpoch, " 1")]
    [TestCase("%Oe", "ja-JP", SundayYearEnd, "31")]
    [TestCase("%Oe", "ja-JP", IsoWeekYearBoundary, " 1")]
    [TestCase("%Oe", "es-ES", LeapDay, "29")]
    [TestCase("%Oe", "es-ES", UnixEpoch, " 1")]
    [TestCase("%Oe", "es-ES", SundayYearEnd, "31")]
    [TestCase("%Oe", "es-ES", IsoWeekYearBoundary, " 1")]
    [TestCase("%Oe", "ar-SA", LeapDay, "٢٩")]
    [TestCase("%Oe", "ar-SA", UnixEpoch, " ١")]
    [TestCase("%Oe", "ar-SA", SundayYearEnd, "٣١")]
    [TestCase("%Oe", "ar-SA", IsoWeekYearBoundary, " ١")]
    public void TestStrftime_Oe(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OH", "C", LeapDay, "23")]
    [TestCase("%OH", "C", UnixEpoch, "00")]
    [TestCase("%OH", "C", SundayYearEnd, "13")]
    [TestCase("%OH", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%OH", "en-GB", LeapDay, "23")]
    [TestCase("%OH", "en-GB", UnixEpoch, "00")]
    [TestCase("%OH", "en-GB", SundayYearEnd, "13")]
    [TestCase("%OH", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%OH", "ja-JP", LeapDay, "23")]
    [TestCase("%OH", "ja-JP", UnixEpoch, "00")]
    [TestCase("%OH", "ja-JP", SundayYearEnd, "13")]
    [TestCase("%OH", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%OH", "es-ES", LeapDay, "23")]
    [TestCase("%OH", "es-ES", UnixEpoch, "00")]
    [TestCase("%OH", "es-ES", SundayYearEnd, "13")]
    [TestCase("%OH", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%OH", "ar-SA", LeapDay, "٢٣")]
    [TestCase("%OH", "ar-SA", UnixEpoch, "٠٠")]
    [TestCase("%OH", "ar-SA", SundayYearEnd, "١٣")]
    [TestCase("%OH", "ar-SA", IsoWeekYearBoundary, "٠٠")]
    public void TestStrftime_OH(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OI", "C", LeapDay, "11")]
    [TestCase("%OI", "C", UnixEpoch, "12")]
    [TestCase("%OI", "C", SundayYearEnd, "01")]
    [TestCase("%OI", "C", IsoWeekYearBoundary, "12")]
    [TestCase("%OI", "en-GB", LeapDay, "11")]
    [TestCase("%OI", "en-GB", UnixEpoch, "12")]
    [TestCase("%OI", "en-GB", SundayYearEnd, "01")]
    [TestCase("%OI", "en-GB", IsoWeekYearBoundary, "12")]
    [TestCase("%OI", "ja-JP", LeapDay, "11")]
    [TestCase("%OI", "ja-JP", UnixEpoch, "12")]
    [TestCase("%OI", "ja-JP", SundayYearEnd, "01")]
    [TestCase("%OI", "ja-JP", IsoWeekYearBoundary, "12")]
    [TestCase("%OI", "es-ES", LeapDay, "11")]
    [TestCase("%OI", "es-ES", UnixEpoch, "12")]
    [TestCase("%OI", "es-ES", SundayYearEnd, "01")]
    [TestCase("%OI", "es-ES", IsoWeekYearBoundary, "12")]
    [TestCase("%OI", "ar-SA", LeapDay, "١١")]
    [TestCase("%OI", "ar-SA", UnixEpoch, "١٢")]
    [TestCase("%OI", "ar-SA", SundayYearEnd, "٠١")]
    [TestCase("%OI", "ar-SA", IsoWeekYearBoundary, "١٢")]
    public void TestStrftime_OI(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Om", "C", LeapDay, "02")]
    [TestCase("%Om", "C", UnixEpoch, "01")]
    [TestCase("%Om", "C", SundayYearEnd, "12")]
    [TestCase("%Om", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%Om", "en-GB", LeapDay, "02")]
    [TestCase("%Om", "en-GB", UnixEpoch, "01")]
    [TestCase("%Om", "en-GB", SundayYearEnd, "12")]
    [TestCase("%Om", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%Om", "ja-JP", LeapDay, "02")]
    [TestCase("%Om", "ja-JP", UnixEpoch, "01")]
    [TestCase("%Om", "ja-JP", SundayYearEnd, "12")]
    [TestCase("%Om", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%Om", "es-ES", LeapDay, "02")]
    [TestCase("%Om", "es-ES", UnixEpoch, "01")]
    [TestCase("%Om", "es-ES", SundayYearEnd, "12")]
    [TestCase("%Om", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%Om", "ar-SA", LeapDay, "٠٢")]
    [TestCase("%Om", "ar-SA", UnixEpoch, "٠١")]
    [TestCase("%Om", "ar-SA", SundayYearEnd, "١٢")]
    [TestCase("%Om", "ar-SA", IsoWeekYearBoundary, "٠١")]
    public void TestStrftime_Om(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OM", "C", LeapDay, "05")]
    [TestCase("%OM", "C", UnixEpoch, "00")]
    [TestCase("%OM", "C", SundayYearEnd, "14")]
    [TestCase("%OM", "C", IsoWeekYearBoundary, "01")]
    [TestCase("%OM", "en-GB", LeapDay, "05")]
    [TestCase("%OM", "en-GB", UnixEpoch, "00")]
    [TestCase("%OM", "en-GB", SundayYearEnd, "14")]
    [TestCase("%OM", "en-GB", IsoWeekYearBoundary, "01")]
    [TestCase("%OM", "ja-JP", LeapDay, "05")]
    [TestCase("%OM", "ja-JP", UnixEpoch, "00")]
    [TestCase("%OM", "ja-JP", SundayYearEnd, "14")]
    [TestCase("%OM", "ja-JP", IsoWeekYearBoundary, "01")]
    [TestCase("%OM", "es-ES", LeapDay, "05")]
    [TestCase("%OM", "es-ES", UnixEpoch, "00")]
    [TestCase("%OM", "es-ES", SundayYearEnd, "14")]
    [TestCase("%OM", "es-ES", IsoWeekYearBoundary, "01")]
    [TestCase("%OM", "ar-SA", LeapDay, "٠٥")]
    [TestCase("%OM", "ar-SA", UnixEpoch, "٠٠")]
    [TestCase("%OM", "ar-SA", SundayYearEnd, "١٤")]
    [TestCase("%OM", "ar-SA", IsoWeekYearBoundary, "٠١")]
    public void TestStrftime_OM(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OS", "C", LeapDay, "07")]
    [TestCase("%OS", "C", UnixEpoch, "00")]
    [TestCase("%OS", "C", SundayYearEnd, "15")]
    [TestCase("%OS", "C", IsoWeekYearBoundary, "02")]
    [TestCase("%OS", "en-GB", LeapDay, "07")]
    [TestCase("%OS", "en-GB", UnixEpoch, "00")]
    [TestCase("%OS", "en-GB", SundayYearEnd, "15")]
    [TestCase("%OS", "en-GB", IsoWeekYearBoundary, "02")]
    [TestCase("%OS", "ja-JP", LeapDay, "07")]
    [TestCase("%OS", "ja-JP", UnixEpoch, "00")]
    [TestCase("%OS", "ja-JP", SundayYearEnd, "15")]
    [TestCase("%OS", "ja-JP", IsoWeekYearBoundary, "02")]
    [TestCase("%OS", "es-ES", LeapDay, "07")]
    [TestCase("%OS", "es-ES", UnixEpoch, "00")]
    [TestCase("%OS", "es-ES", SundayYearEnd, "15")]
    [TestCase("%OS", "es-ES", IsoWeekYearBoundary, "02")]
    [TestCase("%OS", "ar-SA", LeapDay, "٠٧")]
    [TestCase("%OS", "ar-SA", UnixEpoch, "٠٠")]
    [TestCase("%OS", "ar-SA", SundayYearEnd, "١٥")]
    [TestCase("%OS", "ar-SA", IsoWeekYearBoundary, "٠٢")]
    public void TestStrftime_OS(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Ou", "C", LeapDay, "2")]
    [TestCase("%Ou", "C", UnixEpoch, "4")]
    [TestCase("%Ou", "C", SundayYearEnd, "7")]
    [TestCase("%Ou", "C", IsoWeekYearBoundary, "5")]
    [TestCase("%Ou", "en-GB", LeapDay, "2")]
    [TestCase("%Ou", "en-GB", UnixEpoch, "4")]
    [TestCase("%Ou", "en-GB", SundayYearEnd, "7")]
    [TestCase("%Ou", "en-GB", IsoWeekYearBoundary, "5")]
    [TestCase("%Ou", "ja-JP", LeapDay, "2")]
    [TestCase("%Ou", "ja-JP", UnixEpoch, "4")]
    [TestCase("%Ou", "ja-JP", SundayYearEnd, "7")]
    [TestCase("%Ou", "ja-JP", IsoWeekYearBoundary, "5")]
    [TestCase("%Ou", "es-ES", LeapDay, "2")]
    [TestCase("%Ou", "es-ES", UnixEpoch, "4")]
    [TestCase("%Ou", "es-ES", SundayYearEnd, "7")]
    [TestCase("%Ou", "es-ES", IsoWeekYearBoundary, "5")]
    [TestCase("%Ou", "ar-SA", LeapDay, "٢")]
    [TestCase("%Ou", "ar-SA", UnixEpoch, "٤")]
    [TestCase("%Ou", "ar-SA", SundayYearEnd, "٧")]
    [TestCase("%Ou", "ar-SA", IsoWeekYearBoundary, "٥")]
    public void TestStrftime_Ou(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OU", "C", LeapDay, "09")]
    [TestCase("%OU", "C", UnixEpoch, "00")]
    [TestCase("%OU", "C", SundayYearEnd, "53")]
    [TestCase("%OU", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%OU", "en-GB", LeapDay, "09")]
    [TestCase("%OU", "en-GB", UnixEpoch, "00")]
    [TestCase("%OU", "en-GB", SundayYearEnd, "53")]
    [TestCase("%OU", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%OU", "ja-JP", LeapDay, "09")]
    [TestCase("%OU", "ja-JP", UnixEpoch, "00")]
    [TestCase("%OU", "ja-JP", SundayYearEnd, "53")]
    [TestCase("%OU", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%OU", "es-ES", LeapDay, "09")]
    [TestCase("%OU", "es-ES", UnixEpoch, "00")]
    [TestCase("%OU", "es-ES", SundayYearEnd, "53")]
    [TestCase("%OU", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%OU", "ar-SA", LeapDay, "٠٩")]
    [TestCase("%OU", "ar-SA", UnixEpoch, "٠٠")]
    [TestCase("%OU", "ar-SA", SundayYearEnd, "٥٣")]
    [TestCase("%OU", "ar-SA", IsoWeekYearBoundary, "٠٠")]
    public void TestStrftime_OU(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OV", "C", LeapDay, "09")]
    [TestCase("%OV", "C", UnixEpoch, "01")]
    [TestCase("%OV", "C", SundayYearEnd, "52")]
    [TestCase("%OV", "C", IsoWeekYearBoundary, "53")]
    [TestCase("%OV", "en-GB", LeapDay, "09")]
    [TestCase("%OV", "en-GB", UnixEpoch, "01")]
    [TestCase("%OV", "en-GB", SundayYearEnd, "52")]
    [TestCase("%OV", "en-GB", IsoWeekYearBoundary, "53")]
    [TestCase("%OV", "ja-JP", LeapDay, "09")]
    [TestCase("%OV", "ja-JP", UnixEpoch, "01")]
    [TestCase("%OV", "ja-JP", SundayYearEnd, "52")]
    [TestCase("%OV", "ja-JP", IsoWeekYearBoundary, "53")]
    [TestCase("%OV", "es-ES", LeapDay, "09")]
    [TestCase("%OV", "es-ES", UnixEpoch, "01")]
    [TestCase("%OV", "es-ES", SundayYearEnd, "52")]
    [TestCase("%OV", "es-ES", IsoWeekYearBoundary, "53")]
    [TestCase("%OV", "ar-SA", LeapDay, "٠٩")]
    [TestCase("%OV", "ar-SA", UnixEpoch, "٠١")]
    [TestCase("%OV", "ar-SA", SundayYearEnd, "٥٢")]
    [TestCase("%OV", "ar-SA", IsoWeekYearBoundary, "٥٣")]
    public void TestStrftime_OV(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Ow", "C", LeapDay, "2")]
    [TestCase("%Ow", "C", UnixEpoch, "4")]
    [TestCase("%Ow", "C", SundayYearEnd, "0")]
    [TestCase("%Ow", "C", IsoWeekYearBoundary, "5")]
    [TestCase("%Ow", "en-GB", LeapDay, "2")]
    [TestCase("%Ow", "en-GB", UnixEpoch, "4")]
    [TestCase("%Ow", "en-GB", SundayYearEnd, "0")]
    [TestCase("%Ow", "en-GB", IsoWeekYearBoundary, "5")]
    [TestCase("%Ow", "ja-JP", LeapDay, "2")]
    [TestCase("%Ow", "ja-JP", UnixEpoch, "4")]
    [TestCase("%Ow", "ja-JP", SundayYearEnd, "0")]
    [TestCase("%Ow", "ja-JP", IsoWeekYearBoundary, "5")]
    [TestCase("%Ow", "es-ES", LeapDay, "2")]
    [TestCase("%Ow", "es-ES", UnixEpoch, "4")]
    [TestCase("%Ow", "es-ES", SundayYearEnd, "0")]
    [TestCase("%Ow", "es-ES", IsoWeekYearBoundary, "5")]
    [TestCase("%Ow", "ar-SA", LeapDay, "٢")]
    [TestCase("%Ow", "ar-SA", UnixEpoch, "٤")]
    [TestCase("%Ow", "ar-SA", SundayYearEnd, "٠")]
    [TestCase("%Ow", "ar-SA", IsoWeekYearBoundary, "٥")]
    public void TestStrftime_Ow(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%OW", "C", LeapDay, "09")]
    [TestCase("%OW", "C", UnixEpoch, "00")]
    [TestCase("%OW", "C", SundayYearEnd, "52")]
    [TestCase("%OW", "C", IsoWeekYearBoundary, "00")]
    [TestCase("%OW", "en-GB", LeapDay, "09")]
    [TestCase("%OW", "en-GB", UnixEpoch, "00")]
    [TestCase("%OW", "en-GB", SundayYearEnd, "52")]
    [TestCase("%OW", "en-GB", IsoWeekYearBoundary, "00")]
    [TestCase("%OW", "ja-JP", LeapDay, "09")]
    [TestCase("%OW", "ja-JP", UnixEpoch, "00")]
    [TestCase("%OW", "ja-JP", SundayYearEnd, "52")]
    [TestCase("%OW", "ja-JP", IsoWeekYearBoundary, "00")]
    [TestCase("%OW", "es-ES", LeapDay, "09")]
    [TestCase("%OW", "es-ES", UnixEpoch, "00")]
    [TestCase("%OW", "es-ES", SundayYearEnd, "52")]
    [TestCase("%OW", "es-ES", IsoWeekYearBoundary, "00")]
    [TestCase("%OW", "ar-SA", LeapDay, "٠٩")]
    [TestCase("%OW", "ar-SA", UnixEpoch, "٠٠")]
    [TestCase("%OW", "ar-SA", SundayYearEnd, "٥٢")]
    [TestCase("%OW", "ar-SA", IsoWeekYearBoundary, "٠٠")]
    public void TestStrftime_OW(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    [TestCase("%Oy", "C", LeapDay, "00")]
    [TestCase("%Oy", "C", UnixEpoch, "70")]
    [TestCase("%Oy", "C", SundayYearEnd, "23")]
    [TestCase("%Oy", "C", IsoWeekYearBoundary, "16")]
    [TestCase("%Oy", "en-GB", LeapDay, "00")]
    [TestCase("%Oy", "en-GB", UnixEpoch, "70")]
    [TestCase("%Oy", "en-GB", SundayYearEnd, "23")]
    [TestCase("%Oy", "en-GB", IsoWeekYearBoundary, "16")]
    [TestCase("%Oy", "ja-JP", LeapDay, "00")]
    [TestCase("%Oy", "ja-JP", UnixEpoch, "70")]
    [TestCase("%Oy", "ja-JP", SundayYearEnd, "23")]
    [TestCase("%Oy", "ja-JP", IsoWeekYearBoundary, "16")]
    [TestCase("%Oy", "es-ES", LeapDay, "00")]
    [TestCase("%Oy", "es-ES", UnixEpoch, "70")]
    [TestCase("%Oy", "es-ES", SundayYearEnd, "23")]
    [TestCase("%Oy", "es-ES", IsoWeekYearBoundary, "16")]
    [TestCase("%Oy", "ar-SA", LeapDay, "٠٠")]
    [TestCase("%Oy", "ar-SA", UnixEpoch, "٧٠")]
    [TestCase("%Oy", "ar-SA", SundayYearEnd, "٢٣")]
    [TestCase("%Oy", "ar-SA", IsoWeekYearBoundary, "١٦")]
    public void TestStrftime_Oy(string formatString, string culture, long unixTimestamp, string expected)
    {
        RunTest(formatString, culture, unixTimestamp, expected);
    }

    private static unsafe void RunTest(string formatString, string culture, long unixTimestamp, string expected)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        
        DateTimeOffset offset = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        using LuaState state = new();
        Span<byte> output = stackalloc byte[100];
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(formatString);
        int len = Lua.strftime(state, output, ref input, offset);

        string result = Encoding.UTF8.GetString(output[..len]);
        Assert.That(result, Is.EqualTo(expected));
    }
}
