import React from "react";

import { ApiError } from "../../Domain/ApiBase/ApiError";

import styles from "./ErrorHandlingContainer.less";
import { ErrorHandlingContainerModal } from "./ErrorHandlingContainerModal";
import { ErrorModalProps } from "./GenericErrorHandlingContainer";

interface DefaultErrorModalProps extends ErrorModalProps {
    showMessageFromServer: boolean;
}

export function DefaultErrorModal({ onClose, isFatal, error, stack, showMessageFromServer }: DefaultErrorModalProps) {
    return (
        <ErrorHandlingContainerModal
            canClose={!isFatal}
            onClose={onClose}
            errorModalTitle="Произошла непредвиденная ошибка"
            showMessageFromServerByDefault={showMessageFromServer}
            message={error == null ? "" : error.message || error.toString()}
            stack={stack}
            serverStack={error instanceof ApiError ? error.serverStackTrace : null}>
            <div className={styles.content}>
                <p>Попробуйте повторить запрос или обновить страницу через некоторое время.</p>
            </div>
        </ErrorHandlingContainerModal>
    );
}
