import _ from "lodash";
import React from "react";
import { Link } from "react-router-dom";

import { ObjectIdentifier } from "../../Domain/Api/DataTypes/ObjectIdentifier";
import { StringUtils } from "../../Domain/Utils/StringUtils";

import styles from "./ObjectTypes.less";

interface ObjectTypesProps {
    objects: ObjectIdentifier[];
    filter: string;
    identifierKeywords: string[];
    getPath: (id: string) => string;
}

export class ObjectTypes extends React.Component<ObjectTypesProps> {
    public getGrouped(objects: ObjectIdentifier[]): Array<[string, ObjectIdentifier[]]> {
        return _(objects)
            .orderBy(item => this.getIdentifierWithoutKeywords(item.identifier).toUpperCase())
            .groupBy(item => this.getIdentifierWithoutKeywords(item.identifier)[0].toUpperCase())
            .toPairs()
            .value();
    }

    public getIdentifierWithoutKeywords(identifier: string): string {
        const { identifierKeywords } = this.props;
        let result = identifier;
        for (const keyword of identifierKeywords) {
            result = result.replace(keyword, "");
        }
        return result;
    }

    public getFiltered(objects: ObjectIdentifier[], filter: string): ObjectIdentifier[] {
        return objects.filter(item => StringUtils.checkWordByCase(item.identifier, filter));
    }

    public renderIdentifier(identifier: string, identifierKeywords: string[]): string | JSX.Element {
        if (identifierKeywords.length === 0) {
            return identifier;
        }

        const [first, ...rest] = identifierKeywords;
        if (identifier.includes(first)) {
            const splitByKeyword = identifier.split(first);
            return (
                <>
                    {splitByKeyword.map((item, i) => (
                        <>
                            {this.renderIdentifier(item, rest)}
                            {i < splitByKeyword.length - 1 && <span className={styles.mutedKeyword}>{first}</span>}
                        </>
                    ))}
                </>
            );
        }
        return this.renderIdentifier(identifier, rest);
    }

    public renderItem(item: ObjectIdentifier): JSX.Element {
        const { getPath, identifierKeywords } = this.props;
        return (
            <div key={item.identifier} data-tid="ObjectItem">
                <Link className={styles.routerLink} to={getPath(item.identifier)} data-tid="ObjectLink">
                    {this.renderIdentifier(item.identifier, identifierKeywords)}
                </Link>
            </div>
        );
    }

    public renderTypes(objects: ObjectIdentifier[], displayGroups: boolean): JSX.Element {
        if (!displayGroups) {
            return (
                <div className={styles.root} data-tid="ObjectsList">
                    {objects.map(item => this.renderItem(item))}
                </div>
            );
        }

        const groupedObjects = this.getGrouped(objects);
        return (
            <div data-tid="ObjectsList" className={styles.root}>
                {groupedObjects.map(([firstLetter, identifiers], key) => (
                    <div className={styles.typeGroup} key={key}>
                        <div className={styles.firstLetter} data-tid="FirstLetter">
                            {firstLetter}
                        </div>
                        {identifiers.map(item => this.renderItem(item))}
                    </div>
                ))}
            </div>
        );
    }

    public renderSchema(schemaName: string, objects: ObjectIdentifier[]) {
        const { filter } = this.props;
        const schema = objects[0]?.schemaDescription;
        let filteredObjects = objects;
        const emptyFilter = StringUtils.isNullOrWhitespace(filter);
        if (!emptyFilter) {
            filteredObjects = this.getFiltered(objects, filter);
        }
        return (
            filteredObjects.length !== 0 && (
                <div data-tid="ObjectGroup">
                    <div className={styles.schema}>
                        <span data-tid="Name">{schemaName}</span>{" "}
                        {schema.allowReadAll && (
                            <span className={styles.indexed} data-tid="IndexedLabel">
                                indexed
                            </span>
                        )}
                    </div>
                    <div>{this.renderTypes(filteredObjects, emptyFilter)}</div>
                </div>
            )
        );
    }

    public render(): JSX.Element {
        const categorized = _.groupBy(this.props.objects, x => x.schemaDescription.schemaName);
        return (
            <div data-tid="ObjectGroups">
                {Object.keys(categorized).map(schemaName => this.renderSchema(schemaName, categorized[schemaName]))}
            </div>
        );
    }
}
