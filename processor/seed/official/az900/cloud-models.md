---
layout: ModuleUnit
title: Define cloud models - Training | Microsoft Learn
canonicalUrl: https://learn.microsoft.com/en-us/training/modules/describe-cloud-compute/5-define-cloud-models
uid: learn.wwl.describe-cloud-compute.define-cloud-models
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
description: Define cloud models
ms.date: 2024-12-12T00:00:00.0000000Z
author: wwlpublish
ms.author: robbarefoot
ms.topic: unit
ms.custom:
- N/A
locale: en-us
document_id: bc9d591f-3aea-16bb-c326-1cdaa7643aa9
document_version_independent_id: f028717d-f73d-94c9-86f6-d6e4af4a4b1c
updated_at: 2025-03-28T23:01:00.0000000Z
original_content_git_url: https://github.com/MicrosoftDocs/learn-pr/blob/live/learn-pr/wwl-azure/describe-cloud-compute/5-define-cloud-models.yml
gitcommit: https://github.com/MicrosoftDocs/learn-pr/blob/2b643d7ee1c7d37ae4d56d4bd5b7971fad6ee3ee/learn-pr/wwl-azure/describe-cloud-compute/5-define-cloud-models.yml
git_commit_id: 2b643d7ee1c7d37ae4d56d4bd5b7971fad6ee3ee
site_name: Docs
depot_name: Docs.learn-pr
unit_completion_type: view
feedback_product_url: ''
feedback_help_link_type: ''
feedback_help_link_url: ''
ROBOTS: noindex
asset_id: modules/describe-cloud-compute/5-define-cloud-models
moniker_range_name: 
monikers: []
item_type: Content
source_path: learn-pr/wwl-azure/describe-cloud-compute/5-define-cloud-models.yml
platformId: 3d99b556-fdfe-b842-8481-47c9128abb58
---

# Define cloud models

Completed

- 4 minutes

What are cloud models? The cloud models define the deployment type of cloud resources. The three main cloud models are: private, public, and hybrid.

![Diagram showing four cloud deployment models with key characteristics for each.](../../wwl-azure/describe-cloud-compute/media/cloud-deployment-models.png)

## Private cloud

A private cloud is a cloud environment used by a single entity. It evolved naturally from the traditional datacenter model, delivering IT services over the internet while keeping resources dedicated to one organization. Private cloud provides much greater control for your IT team. However, it also comes with greater cost and fewer of the benefits of a public cloud deployment. A private cloud may be hosted from your on-site datacenter, or in a dedicated datacenter offsite, potentially even by a third party.

## Public cloud

A public cloud is built, controlled, and maintained by a third-party cloud provider. With a public cloud, anyone that wants to purchase cloud services can access and use resources. The general public availability is a key difference between public and private clouds.

## Hybrid cloud

A hybrid cloud is a computing environment that uses both public and private clouds in an inter-connected environment. A hybrid cloud environment can be used to allow a private cloud to surge for increased, temporary demand by deploying public cloud resources. Hybrid cloud can be used to provide an extra layer of security. For example, users can flexibly choose which services to keep in public cloud and which to deploy to their private cloud infrastructure.

The following table highlights a few key comparative aspects between the cloud models.

| **Public cloud** | **Private cloud** | **Hybrid cloud** |
| --- | --- | --- |
| No capital expenditures to scale up | You have complete control over resources and security | Provides the most flexibility |
| Applications can be quickly provisioned and deprovisioned | Data isn't collocated with other tenants' data | You determine where to run your applications |
| You pay only for what you use | Hardware must be purchased for startup and maintenance | You control security, compliance, or legal requirements |
| You don't have complete control over resources and security | You are responsible for hardware maintenance and updates |  |

## Multicloud

A fourth, and increasingly likely scenario is a multicloud scenario. In a multicloud scenario, you use multiple public cloud providers. Maybe you use different features from different cloud providers. Or maybe you started your cloud journey with one provider and are in the process of migrating to a different provider. Regardless, in a multicloud environment you deal with two (or more) public cloud providers and manage resources and security in both environments.

## Azure Arc

Azure Arc is a set of technologies that helps manage your cloud environment. Azure Arc can help manage your cloud environment whether it's a public cloud solely on Azure, a private cloud in your datacenter, a hybrid configuration, or even a multicloud environment running on multiple cloud providers at once.

## Azure VMware Solution

What if you're already established with VMware in a private cloud environment but want to migrate to a public or hybrid cloud? Azure VMware Solution lets you run your VMware workloads in Azure with seamless integration and scalability.