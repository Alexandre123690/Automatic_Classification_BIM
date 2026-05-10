"""
Train all 8 ML models and save PKLs + encoders for inference.
Models: DecisionTree, RandomForest, XGBoost, SVM x 2 datasets
"""

import os
import joblib
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.preprocessing import LabelEncoder
from sklearn.impute import SimpleImputer
from sklearn.model_selection import train_test_split
from sklearn.tree import DecisionTreeClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.svm import SVC
from xgboost import XGBClassifier
from imblearn.over_sampling import SMOTE

# ── Paths ─────────────────────────────────────────────────────────────────────
BASE_DIR = Path(__file__).parent
MODELS_DIR = BASE_DIR / "models"
MODELS_DIR.mkdir(exist_ok=True)

DATA1 = BASE_DIR / "data4_1.csv"
DATA2 = BASE_DIR / "data4_2.csv"

# ── Features ──────────────────────────────────────────────────────────────────
FEATURES = [
    "Volume", "Area", "Length", "Height", "Thickness/Width",
    "Base_offset", "Top_offset", "Number_of_Faces",
    "Bounding_Box_Width", "Bounding_Box_Height", "Bounding_Box_Depth",
    "Centroid_X", "Centroid_Y", "Centroid_Z",
    "Orientation_Angle", "Curvature",
    "Category_Curtain Panels", "Category_Doors", "Category_Floors",
    "Category_Roofs", "Category_Stairs", "Category_Structural Columns",
    "Category_Structural Foundations", "Category_Structural Framing",
    "Category_Walls", "Category_Windows",
    "load_bearing_status_binary",
]

TARGETS = {
    "data4_1.csv": "SECClasS_Code_Ss_1_encode",
    "data4_2.csv": "SECClasS_Code_Ss_2_encode",
}

ORIGINAL_TARGETS = {
    "data4_1.csv": "SECClasS_Code_Ss_1",
    "data4_2.csv": "SECClasS_Code_Ss_2",
}


def build_xy(df, target_col):
    available = [f for f in FEATURES if f in df.columns]
    X = df[available].copy()
    X.replace([np.inf, -np.inf], np.nan, inplace=True)
    y = df[target_col].values
    return X, y


def train_and_save(dataset_path: Path, suffix: str):
    print(f"\n{'='*60}")
    print(f"Processing {dataset_path.name}  →  suffix={suffix}")
    print('='*60)

    df = pd.read_csv(dataset_path)
    target_col = TARGETS[dataset_path.name]
    orig_col = ORIGINAL_TARGETS[dataset_path.name]

    # ── Encoder ──────────────────────────────────────────────────────────────
    encoder = LabelEncoder()
    if orig_col in df.columns:
        encoder.fit(df[orig_col].astype(str))
    else:
        encoder.fit(df[target_col].astype(str))
    enc_path = MODELS_DIR / f"encoder_{suffix}.pkl"
    joblib.dump(encoder, enc_path)
    print(f"  [OK] Encoder saved → {enc_path.name}")

    # ── Features / target ────────────────────────────────────────────────────
    X, y = build_xy(df, target_col)

    # ── Imputer ──────────────────────────────────────────────────────────────
    imputer = SimpleImputer(strategy="median")
    X_imp = imputer.fit_transform(X)
    imp_path = MODELS_DIR / f"imputer_{suffix}.pkl"
    joblib.dump(imputer, imp_path)
    print(f"  [OK] Imputer saved → {imp_path.name}")

    # Save feature names used
    feat_path = MODELS_DIR / f"features_{suffix}.pkl"
    joblib.dump(list(X.columns), feat_path)
    print(f"  [OK] Feature list saved → {feat_path.name}")

    # ── Train / test split ───────────────────────────────────────────────────
    X_train, X_test, y_train, y_test = train_test_split(
        X_imp, y, test_size=0.2, random_state=42, stratify=y
    )

    # ── SMOTE ────────────────────────────────────────────────────────────────
    try:
        smote = SMOTE(random_state=42)
        X_res, y_res = smote.fit_resample(X_train, y_train)
        print(f"  SMOTE: {X_train.shape[0]} → {X_res.shape[0]} samples")
    except Exception as e:
        print(f"  SMOTE failed ({e}), using original data")
        X_res, y_res = X_train, y_train

    # ── Decision Tree ─────────────────────────────────────────────────────────
    print(f"  Training DecisionTree...")
    dt = DecisionTreeClassifier(random_state=42)
    dt.fit(X_res, y_res)
    joblib.dump(dt, MODELS_DIR / f"dt_{suffix}.pkl")
    print(f"  [OK] DecisionTree saved → dt_{suffix}.pkl")

    # ── Random Forest ─────────────────────────────────────────────────────────
    print(f"  Training RandomForest...")
    rf = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=-1)
    rf.fit(X_res, y_res)
    joblib.dump(rf, MODELS_DIR / f"rf_{suffix}.pkl")
    print(f"  [OK] RandomForest saved → rf_{suffix}.pkl")

    # ── XGBoost ───────────────────────────────────────────────────────────────
    print(f"  Training XGBoost...")
    xgb = XGBClassifier(
        n_estimators=100, random_state=42, n_jobs=-1,
        use_label_encoder=False, eval_metric="mlogloss", verbosity=0
    )
    xgb.fit(X_res, y_res)
    joblib.dump(xgb, MODELS_DIR / f"xgb_{suffix}.pkl")
    print(f"  [OK] XGBoost saved → xgb_{suffix}.pkl")

    # ── SVM ───────────────────────────────────────────────────────────────────
    print(f"  Training SVM (this may take a while)...")
    svm = SVC(kernel="rbf", probability=True, random_state=42, max_iter=2000)
    svm.fit(X_res, y_res)
    joblib.dump(svm, MODELS_DIR / f"svm_{suffix}.pkl")
    print(f"  [OK] SVM saved → svm_{suffix}.pkl")


if __name__ == "__main__":
    train_and_save(DATA1, "4_1")
    train_and_save(DATA2, "4_2")

    print("\n" + "="*60)
    print("ALL DONE — models saved in Final/models/")
    print("="*60)
    files = sorted(MODELS_DIR.iterdir())
    for f in files:
        print(f"  {f.name}")
