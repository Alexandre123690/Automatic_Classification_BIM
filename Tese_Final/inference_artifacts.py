from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import pandas as pd


def resolve_final_dir(base_dir: str | Path | None = None) -> Path:
    if base_dir is not None:
        return Path(base_dir)

    cwd = Path.cwd()
    candidates = [cwd, cwd / "Final"]
    for candidate in candidates:
        if (
            (candidate / "Final_Preparation.ipynb").exists()
            or (candidate / "data4_1.csv").exists()
            or (candidate / "data4_2.csv").exists()
        ):
            return candidate
    return cwd


def ensure_artifact_dirs(base_dir: str | Path | None = None) -> dict[str, Path]:
    final_dir = resolve_final_dir(base_dir)
    artifacts_dir = final_dir / "artifacts"
    models_dir = artifacts_dir / "models"
    encoders_dir = artifacts_dir / "encoders"

    models_dir.mkdir(parents=True, exist_ok=True)
    encoders_dir.mkdir(parents=True, exist_ok=True)

    return {
        "base": final_dir,
        "artifacts": artifacts_dir,
        "models": models_dir,
        "encoders": encoders_dir,
    }


def _serialize_class_labels(class_labels: Any) -> list[Any] | None:
    if class_labels is None:
        return None
    if isinstance(class_labels, np.ndarray):
        return class_labels.tolist()
    if isinstance(class_labels, (list, tuple, pd.Index)):
        return list(class_labels)
    return list(class_labels)


def save_model_bundle(
    name: str,
    estimator: Any,
    imputer: Any,
    features: list[str] | tuple[str, ...] | pd.Index,
    target_column: str,
    class_labels: Any = None,
    metrics: dict[str, Any] | None = None,
    metadata: dict[str, Any] | None = None,
    base_dir: str | Path | None = None,
) -> Path:
    dirs = ensure_artifact_dirs(base_dir)
    output_path = dirs["models"] / f"{name}.pkl"

    bundle = {
        "name": name,
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "estimator": estimator,
        "imputer": imputer,
        "features": list(features),
        "target_column": target_column,
        "class_labels": _serialize_class_labels(class_labels),
        "metrics": metrics or {},
        "metadata": metadata or {},
    }

    joblib.dump(bundle, output_path)
    return output_path


def save_label_encoder(
    name: str,
    encoder: Any,
    original_column: str,
    encoded_column: str,
    metadata: dict[str, Any] | None = None,
    base_dir: str | Path | None = None,
) -> Path:
    dirs = ensure_artifact_dirs(base_dir)
    output_path = dirs["encoders"] / f"{name}.pkl"

    bundle = {
        "name": name,
        "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "encoder": encoder,
        "classes": list(getattr(encoder, "classes_", [])),
        "original_column": original_column,
        "encoded_column": encoded_column,
        "metadata": metadata or {},
    }

    joblib.dump(bundle, output_path)
    return output_path


def load_artifact(
    name_or_path: str | Path,
    artifact_type: str = "models",
    base_dir: str | Path | None = None,
) -> dict[str, Any]:
    path = Path(name_or_path)
    if not path.exists():
        dirs = ensure_artifact_dirs(base_dir)
        path = dirs[artifact_type] / f"{name_or_path}.pkl"
    return joblib.load(path)


def prepare_features(bundle: dict[str, Any], dataframe: pd.DataFrame) -> pd.DataFrame:
    missing_columns = [column for column in bundle["features"] if column not in dataframe.columns]
    if missing_columns:
        raise KeyError(f"Faltam colunas para inferência: {missing_columns}")

    X = dataframe[bundle["features"]].copy()
    X = X.replace([np.inf, -np.inf], np.nan)

    imputer = bundle.get("imputer")
    if imputer is None:
        return X

    transformed = imputer.transform(X)
    return pd.DataFrame(transformed, columns=bundle["features"], index=X.index)


def predict_dataframe(
    bundle_or_path: dict[str, Any] | str | Path,
    dataframe: pd.DataFrame,
    return_decoded: bool = False,
    encoder_bundle_or_path: dict[str, Any] | str | Path | None = None,
    base_dir: str | Path | None = None,
) -> pd.DataFrame:
    bundle = (
        bundle_or_path
        if isinstance(bundle_or_path, dict)
        else load_artifact(bundle_or_path, artifact_type="models", base_dir=base_dir)
    )

    X_prepared = prepare_features(bundle, dataframe)
    predictions = bundle["estimator"].predict(X_prepared)

    result = pd.DataFrame({"prediction_encoded": predictions}, index=dataframe.index)

    if return_decoded:
        if encoder_bundle_or_path is None:
            raise ValueError("Para return_decoded=True tens de fornecer o encoder correspondente.")
        encoder_bundle = (
            encoder_bundle_or_path
            if isinstance(encoder_bundle_or_path, dict)
            else load_artifact(encoder_bundle_or_path, artifact_type="encoders", base_dir=base_dir)
        )
        result["prediction_label"] = encoder_bundle["encoder"].inverse_transform(
            np.asarray(predictions, dtype=int)
        )

    return result
