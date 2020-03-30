import DefaultDatePicker from "@skbkontur/react-ui/DatePicker";
import moment from "moment";
import * as React from "react";
import { DateUtils } from "Commons/DateUtils";
import { TimeUtils } from "Commons/TimeUtils";
import { StringUtils } from "Commons/Utils/StringUtils";
import { ICanBeValidated, RussianDateFormat } from "Domain/EDI/DataTypes/DateTimeRange";
import { Time, TimeZone } from "Domain/EDI/DataTypes/Time";

interface DatePickerProps {
    value: Nullable<Date>;
    onChange: (e: SyntheticEvent<any>, value: Nullable<Date>) => void;
    width?: React.ReactText;
    minDate?: Date | string;
    maxDate?: Date | string;
    isHoliday?: (day: string, isWeekend: boolean) => boolean;
    timeZone?: TimeZone | number;
    defaultTime?: Time;
    disabled?: boolean;
    error?: boolean;
}

interface DatePickerState {
    date: string;
}

const DatePickerDefaultProps = {
    width: 120,
    minDate: "01.01.1900",
    maxDate: "31.12.2099",
    isHoliday: (_day: string, isWeekend: boolean) => isWeekend,
};

const defaultTime = "00:00";

export class DatePicker extends React.Component<DatePickerProps, DatePickerState> implements ICanBeValidated {
    public state = {
        date: "",
    };

    public static defaultProps = {
        timeZone: TimeUtils.TimeZones.UTC,
    };

    private datePicker: Nullable<DefaultDatePicker>;

    public componentDidMount() {
        const { value, timeZone } = this.props;
        const stringifiedDate = this.convertDateToStringWithTimezone(value, timeZone);
        this.setState({ date: stringifiedDate });
    }

    public componentDidUpdate(prevProps: DatePickerProps, prevState: DatePickerState) {
        const { value, timeZone } = this.props;
        if (prevProps.value !== this.props.value) {
            const stringifiedDate = this.convertDateToStringWithTimezone(value, timeZone);
            this.setState({ date: stringifiedDate });
        }
    }

    public render(): JSX.Element {
        const { minDate, maxDate, timeZone } = this.props;

        return (
            <DefaultDatePicker
                {...DatePickerDefaultProps}
                {...this.props}
                maxDate={this.convertDateToStringWithTimezone(maxDate, timeZone)}
                minDate={this.convertDateToStringWithTimezone(minDate, timeZone)}
                ref={el => (this.datePicker = el)}
                value={this.state.date}
                onChange={this.handleChange}
            />
        );
    }

    public focus = (): void => {
        if (this.datePicker != null) {
            this.datePicker.focus();
        }
    };

    private readonly handleChange = (e: any, newStringifiedDate: RussianDateFormat): void => {
        this.setState({ date: newStringifiedDate });
        if (StringUtils.isNullOrWhitespace(newStringifiedDate)) {
            this.props.onChange(e, null);
            return;
        }

        if (!DefaultDatePicker.validate(newStringifiedDate)) {
            return;
        }

        const newDate = this.convertStringToDate(newStringifiedDate);
        this.props.onChange(e, newDate);
    };

    private readonly convertStringToDate = (newStringifiedDate: RussianDateFormat): Date => {
        const { timeZone } = this.props;
        const date = DateUtils.convertStringToDate(newStringifiedDate);
        const ISODate = moment(date).format("YYYY-MM-DD");
        const time = this.props.defaultTime || defaultTime;
        const timeZoneOffset = TimeUtils.getTimeZoneOffsetOrDefault(timeZone);
        return moment(`${ISODate}T${time}${TimeUtils.timeZoneOffsetToString(timeZoneOffset)}`).toDate();
    };

    private readonly convertDateToStringWithTimezone = (date: Nullable<Date | string>, timeZone?: number) => {
        const timeZoneOffset = TimeUtils.getTimeZoneOffsetOrDefault(timeZone);
        return date ? DateUtils.convertDateToString(date, timeZoneOffset) : "";
    };
}
