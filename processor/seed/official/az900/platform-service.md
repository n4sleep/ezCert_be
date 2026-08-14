---
layout: ModuleUnit
title: Describe Platform as a Service - Training | Microsoft Learn
canonicalUrl: https://learn.microsoft.com/en-us/training/modules/describe-cloud-service-types/3-describe-platform-service
uid: learn.wwl.describe-cloud-service-types.describe-platform-service
page_type: learn
page_kind: unit
azure_sandbox: false
sandbox: false
breadcrumb_path: /learn/breadcrumb/toc.json
feedback_system: Standard
clicktale: true
uhfHeaderId: MSDocsHeader-Learn
adobe-target: true
prefetch-feature-rollout: true
localization_scopes:
- ja-jp
- ko-kr
- zh-cn
- zh-tw
description: Describe Platform as a Service
ms.date: 2024-12-12T00:00:00.0000000Z
author: wwlpublish
ms.author: robbarefoot
ms.topic: unit
ms.custom:
- N/A
locale: en-us
document_id: 5b639953-e478-dc73-1286-d636a75385ec
document_version_independent_id: 5932f1d0-5951-0a3b-a115-203c3ded36b2
updated_at: 2024-12-12T18:00:00.0000000Z
original_content_git_url: https://github.com/MicrosoftDocs/learn-pr/blob/live/learn-pr/wwl-azure/describe-cloud-service-types/3-describe-platform-service.yml
gitcommit: https://github.com/MicrosoftDocs/learn-pr/blob/b50216d32f233e1c974c50c59341b602adee4748/learn-pr/wwl-azure/describe-cloud-service-types/3-describe-platform-service.yml
git_commit_id: b50216d32f233e1c974c50c59341b602adee4748
site_name: Docs
depot_name: Docs.learn-pr
unit_completion_type: view
feedback_product_url: ''
feedback_help_link_type: ''
feedback_help_link_url: ''
ROBOTS: noindex
asset_id: modules/describe-cloud-service-types/3-describe-platform-service
moniker_range_name: 
monikers: []
item_type: Content
source_path: learn-pr/wwl-azure/describe-cloud-service-types/3-describe-platform-service.yml
platformId: 580eaa6f-f040-8ea3-8237-543f8d9e8d26
---

# Describe Platform as a Service

Completed

- 2 minutes

Platform as a service (PaaS) is a middle ground between renting space in a datacenter (infrastructure as a service) and paying for a complete and deployed solution (software as a service). In a PaaS environment, the cloud provider maintains the physical infrastructure, physical security, and connection to the internet. They also maintain the operating systems, middleware, development tools, and analytics services that make up a cloud solution. In a PaaS scenario, you don't have to worry about the licensing or patching for operating systems and databases.

PaaS is well suited to provide a complete development environment without the headache of maintaining all the development infrastructure.

## Responsibility focus in PaaS

In PaaS, the cloud provider manages the physical infrastructure and platform components such as operating systems, middleware, and managed runtimes. You focus on your application code, data, and access controls. Depending on service configuration, some networking and application security settings are shared.

![Diagram showing PaaS responsibility split with customer managing applications and data and provider managing the platform and infrastructure, plus common scenarios.](../../wwl-azure/describe-cloud-service-types/media/describe-platform-service.png)

## Scenarios

Common scenarios where PaaS might make sense include:

- **Development framework**: PaaS provides a framework that developers can build upon to develop or customize cloud-based applications. Developers can create applications using built-in software components. Cloud features such as scalability, high availability, and multitenant capability are included, reducing the amount of coding that developers must do.
- **Analytics or business intelligence**: Tools provided as a service with PaaS allow teams to analyze and mine their data, find insights and patterns, and predict outcomes to improve planning and operational decisions.