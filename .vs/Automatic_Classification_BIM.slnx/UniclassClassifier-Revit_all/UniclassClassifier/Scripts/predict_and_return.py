# -*- coding: utf-8 -*-
import pandas as pd
import numpy as np
import re
import sys
import joblib
import json
import os
import warnings
warnings.filterwarnings('ignore')

try:
    # Argumentos
    csv_path = sys.argv[1]
    json_path = sys.argv[2]
    model_path = sys.argv[3]
    level1_threshold = float(sys.argv[4]) if len(sys.argv) > 4 else 0.20
    level2_threshold = float(sys.argv[5]) if len(sys.argv) > 5 else 0.40

    base_dir = os.path.dirname(json_path)
    out_csv = os.path.join(base_dir, "datapreparation.csv")
    out_json = os.path.join(base_dir, "classified.json")

    # Carregar dados
    df = pd.read_json(json_path)

    # Eliminar colunas não usadas
    drop_cols = [
        'SECClass_Code_EF', 'SECClass_Title_EF', 'SECClasS_Title_Ss', 'SECClass_Code_Pr', 
        'SECClass_Title_Pr', 'Family and Type', 'Base_constraint', 'Top_constraint', 'Level', 
        'Base_level', 'Top_level', 'Base_offset', 'Top_offset', 'Phase_Created',
        'Start_X', 'Start_Y', 'Start_Z', 'End_X', 'End_Y', 'End_Z', 'Aspect_Ratio', 
        'Volume_to_Surface_Area_Ratio', 'Comments', 'Keynote', 'Description', 'Materials',
        'Category'
    ]

    df.drop(columns=[col for col in drop_cols if col in df.columns], inplace=True)

    # Codificar ElementID
    df['ElementID'] = df['ElementID'].astype('category')

    # load_bearing_status
    df['load_bearing_status'] = df['load_bearing_status'].replace("None", "Non Load-Bearing")
    df['load_bearing_status_binary'] = df['load_bearing_status'].astype('category')
    df = pd.get_dummies(df, columns=['load_bearing_status_binary'], drop_first=True)
    if 'load_bearing_status_binary_Non Load-Bearing' not in df.columns:
        df['load_bearing_status_binary_Non Load-Bearing'] = 0
    df['load_bearing_status_binary_Non Load-Bearing'] = df['load_bearing_status_binary_Non Load-Bearing'].astype(float)

    # Variáveis numéricas
    numeric_cols = [
        'Volume', 'Area', 'Length', 'Height', 'Thickness/Width',
        'Bounding_Box_Width', 'Bounding_Box_Height', 'Bounding_Box_Depth',
        'Centroid_X', 'Centroid_Y', 'Centroid_Z',
        'Orientation_Angle', 'Curvature'
    ]

    for col in numeric_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors='coerce').fillna(0.0).astype(float)

    # Converter 'Number_of_Faces' para inteiro
    df['Number_of_Faces'] = pd.to_numeric(df['Number_of_Faces'], errors="coerce").fillna(0).astype(int)

    # Remover colunas temporárias
    cols_to_remove = ['load_bearing_status', 'SECClasS_Code_Ss']
    df.drop(columns=[col for col in cols_to_remove if col in df.columns], inplace=True)

    # Level 1
    model_l1 = joblib.load(os.path.join(model_path, "model_level1.pkl"))
    encoder_l1 = joblib.load(os.path.join(model_path, "encodertargetlevel1.pkl"))

    # Preparar features Level 1
    X_new = df.drop(columns=["ElementID"])

    # Garantir colunas Level 1
    for col in model_l1.feature_names_in_:
        if col not in X_new.columns:
            X_new[col] = 0

    # Ordenar colunas Level 1
    X_new = X_new[model_l1.feature_names_in_]
    X_new = X_new.apply(pd.to_numeric, errors='coerce').fillna(0.0)

    # Classificação Level 1
    y_pred_l1 = model_l1.predict(X_new)
    y_pred_l1_decoded = encoder_l1.inverse_transform(y_pred_l1)
    try:
        y_pred_proba_l1 = model_l1.predict_proba(X_new)
        confidences_l1 = np.max(y_pred_proba_l1, axis=1)
    except:
        confidences_l1 = np.ones(len(y_pred_l1), dtype=float)

    # Level 2
    X_new_l2 = X_new.copy()

    # Adicionar colunas Ss1
    ss1_columns = ['Ss1_Ss_20', 'Ss1_Ss_25', 'Ss1_Ss_30', 'Ss1_Ss_35', 'Ss1_Ss_37', 'Ss1_Ss_40']
    for col in ss1_columns:
        X_new_l2[col] = 0

    # Preencher Ss1 baseado em Level 1 (usar threshold configurável)
    for idx, (pred, conf) in enumerate(zip(y_pred_l1_decoded, confidences_l1)):
        if conf > level1_threshold:
            col = f'Ss1_Ss_{pred}'
            if col in ss1_columns:
                X_new_l2.iloc[idx, X_new_l2.columns.get_loc(col)] = 1

    # Level 2
    model_l2 = joblib.load(os.path.join(model_path, "model_level2.pkl"))
    encoder_l2 = joblib.load(os.path.join(model_path, "encodertargetlevel2.pkl"))

    # Garantir colunas Level 2
    if hasattr(model_l2, 'feature_names_in_'):
        X_new_l2 = X_new_l2[model_l2.feature_names_in_]

    # Classificação Level 2
    y_pred_l2 = model_l2.predict(X_new_l2)
    y_pred_l2_decoded = encoder_l2.inverse_transform(y_pred_l2)
    try:
        y_pred_proba_l2 = model_l2.predict_proba(X_new_l2)
        confidences_l2 = np.max(y_pred_proba_l2, axis=1)
    except:
        confidences_l2 = np.ones(len(y_pred_l2), dtype=float)

    # Gerar resultados usando thresholds configuráveis
    results = {}
    for idx, (eid, pred_l1, conf_l1, pred_l2, conf_l2) in enumerate(zip(
        df['ElementID'].astype(str), y_pred_l1_decoded, confidences_l1, y_pred_l2_decoded, confidences_l2
    )):
        if conf_l1 <= level1_threshold:
            results[str(eid)] = {
                "classification": "unclassified",
                "confidence": float(conf_l1),
                "level": "none"
            }
        elif conf_l2 > level2_threshold:
            results[str(eid)] = {
                "classification": str(pred_l2),
                "confidence": float(conf_l2),
                "level": "level_2"
            }
        else:
            results[str(eid)] = {
                "classification": str(pred_l1),
                "confidence": float(conf_l1),
                "level": "level_1"
            }

    # Salvar resultados
    with open(out_json, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    sys.exit(0)

except Exception as e:
    sys.stderr.write(f"Erro: {str(e)}")
    sys.exit(1)
