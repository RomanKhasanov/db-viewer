import { Ticks, TimeZone } from "Domain/EDI/DataTypes/Time";

export class TimeUtils {
    public static TimeZones: { [x: string]: TimeZone } = {
        UTC: 0,
        Moscow: 180,
        Yekaterinburg: 300,
    };

    public static ticksToDate(value: Ticks): Date {
        const unixTimestamp = (parseInt(value, 10) - 621355968000000000) / 10000;
        return new Date(unixTimestamp);
    }

    public static dateToTicks(value: Date): Ticks {
        return (value.getTime() * 10000 + 621355968000000000).toString();
    }

    public static getAllTimeZones(): TimeZone[] {
        return [this.TimeZones.UTC, this.TimeZones.Moscow, this.TimeZones.Yekaterinburg];
    }

    public static getTimeZoneOffsetOrDefault(timeZone?: Nullable<TimeZone | number>): number {
        return timeZone != null ? timeZone : -new Date().getTimezoneOffset();
    }

    public static timeZoneOffsetToString(timeZoneOffset: number): string {
        const { floor, abs } = Math;
        const hours = this.padLeft(String(floor(abs(timeZoneOffset) / 60)));
        const minutes = this.padLeft(String(abs(timeZoneOffset) % 60));
        return `${timeZoneOffset < 0 ? "-" : "+"}${hours}:${minutes}`;
    }

    private static readonly padLeft = (str: string) => "00".substring(0, "00".length - str.length) + str;
}
